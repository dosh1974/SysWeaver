// =============================================================================
//  EwAudio2 - The rows of the audio2 texture (512 x 64, the media "audio2")
// =============================================================================
//  An audio2 effect (//  media: audio2) gets the sound as a 512 x 64 texture on
//  iChannel0 / tex: r = mono, g = left, b = right. Read it at uv = (u, v):
//  v = the row (the constants below, pixel centers), u = the band / segment.
//  The EwAudioGet... functions read r (the mono mix); for left / right read
//  .g / .b with texture() and the same constants.
//
//  Timing: the texture is computed at a fixed 60 analysis steps per second of
//  SOUND time (not per frame): the same values at any frame rate, when seeking
//  and in the video export. "A step" below = 1/60 s.
//
//  Rows 0 .. 31 are the values of the moment, rows 32 .. 63 the same values
//  smoothed (add EwAudioDiffuseAddV to a v): s += 0.2 * (x - s) per step, so
//  a change is half way after ~3 steps (50 ms), 90 % after ~10 steps (170 ms).
//
//  Spectrum (log frequency 20 Hz .. 20 kHz, -100 .. -30 dB -> 0 .. 1):
//    EwAudioFrequency512v .. EwAudioFrequency1v: 512, 256, ... 1 bands
//  Waveform (the last 512 samples, 0.5 = silence; a segment = the sample with
//  the largest amplitude, sign kept):
//    EwAudioWave512v .. EwAudioWave1v: 512, 256, ... 1 segments
//  Values (u, v): bass / mid / kick / snare / hi-hat energies, kick / snare /
//  hi-hat pulses, level (RMS), peak, brightness, onset, tempo (BPM / 255,
//  * EwAudioBpmScale = BPM), tempo confidence, beat phase (0 on a beat .. 1),
//  beat pulse (1 on a beat, decays); EwAudioHiHatRowV: hi-hat energy, all u.
//  Each value is described where its constants are defined below.
//
//  EwAudioGetBpm(s), EwAudioGetBassEnergy(s), EwAudioGetBeatPhase(s), ... read
//  a value (s = iChannel0 / tex; an overload with bool smoothed);
//  EwAudioGetFrequency(s, x, level) / EwAudioGetWave(s, x, level) a row.
//  EwAudio2(row): the v of a row. EwAudioGet<N>u(i): the u of band /
//  segment i of a row of N; EwAudioGet<N>u(x): the band at x in 0..1.
//    float bass = texture(iChannel0, vec2(EwAudioBassU, EwAudioBassV)).r;
//    float level = texture(iChannel0, vec2(EwAudioGet64u(uv.x), EwAudioFrequency64v)).r;
//    float calm = texture(iChannel0, vec2(EwAudioGet64u(uv.x), EwAudioFrequency64v + EwAudioDiffuseAddV)).r;
//
//  Requires: -
//  Docs    : EwAudio2.md
// =============================================================================

// -----------------------------------------------------------------------------
//  Spectrum rows 0 .. 9: row r has 512 >> r bands (a band spans 2^r pixels).
//  The x axis (u) is LOG frequency: x = 0 .. 1 is 20 Hz .. 20 kHz (capped at
//  half the sample rate), f(x) = 20 * 1000^x Hz, x(f) = log(f / 20) / log(1000):
//  50 Hz = 0.13, 100 Hz = 0.23, 250 Hz = 0.37, 1 kHz = 0.57, 4 kHz = 0.77,
//  10 kHz = 0.90. Each octave gets the same width (~0.1).
//  The value is the band's power in dB mapped like ShaderToy:
//  (dB + 100) / 70, clamped: 0 = -100 dB or less, 1 = -30 dB or more.
//  A coarser row averages the POWER of 2 bands of the row above (not the
//  mapped values), so a loud narrow peak still shows in a wide band.
//  The FFT has 2048 samples (a bin ~23 Hz at 48 kHz): the lowest bands of the
//  fine rows are narrower than a bin and are interpolated (smooth, not
//  independent). Prefer 64 .. 128 bands for bar displays.
// -----------------------------------------------------------------------------
const float EwAudioFrequency512v = 0.5 / 64.0;      // row 0: 512 bands, ~0.02 octave each
const float EwAudioFrequency256v = 1.5 / 64.0;      // row 1: 256 bands
const float EwAudioFrequency128v = 2.5 / 64.0;      // row 2: 128 bands
const float EwAudioFrequency64v = 3.5 / 64.0;       // row 3: 64 bands, ~1/6 octave each
const float EwAudioFrequency32v = 4.5 / 64.0;       // row 4: 32 bands, ~1/3 octave each (a classic 31 band EQ)
const float EwAudioFrequency16v = 5.5 / 64.0;       // row 5: 16 bands
const float EwAudioFrequency8v = 6.5 / 64.0;        // row 6: 8 bands, ~1.25 octaves each
const float EwAudioFrequency4v = 7.5 / 64.0;        // row 7: 4 bands: 20-112 Hz, 112-632 Hz, 632-3557 Hz, 3.6-20 kHz
const float EwAudioFrequency2v = 8.5 / 64.0;        // row 8: 2 bands: below / above 632 Hz
const float EwAudioFrequency1v = 9.5 / 64.0;        // row 9: 1 band: the power of the whole spectrum

// -----------------------------------------------------------------------------
//  Row 10: bass energy (u < 0.5) | tempo (u >= 0.5)
// -----------------------------------------------------------------------------
// Bass energy, 0 .. 1: the mean power of 20 .. 150 Hz, mapped like the
// spectrum ((dB + 100) / 70). A level, not a hit detector: it stays high
// during a held bass note. For hits use the kick pulse; the smoothed bass
// (+ EwAudioDiffuseAddV) is a good "pump" for sizes / brightness.
const float EwAudioBassU = 0.25;
const float EwAudioBassV = 10.5 / 64.0;
// Tempo: BPM / 255 (* EwAudioBpmScale = BPM). 0 = no tempo yet (silence, the
// first seconds, no rhythm). Range 60 .. 200 BPM, re-estimated 4 times a
// second (it holds in between) from the onsets (see EwAudioOnsetU) of the last
// 6 seconds (autocorrelation, weighted towards 120 BPM: a song at 70 or 240
// BPM may read as its double / half). Check the tempo confidence before use.
// The beat period in seconds = 60 / BPM.
const float EwAudioBpmU = 0.75;
const float EwAudioBpmV = 10.5 / 64.0;
const float EwAudioBpmScale = 255.0;               // tempo value * this = BPM

// -----------------------------------------------------------------------------
//  Row 11: beat phase (u < 0.5) | beat pulse (u >= 0.5)
//  Where the beats fall: the onsets of the last 6 seconds are combed with the
//  tempo's period and the offset that lines up the most onsets is the beat
//  grid. So the grid keeps going through a quiet beat (it predicts), unlike
//  the kick pulse (it reacts). 1 step (1/60 s) resolution. Without a tempo
//  (BPM 0) the grid is a 120 BPM guess: fade beat effects with the tempo
//  confidence.
// -----------------------------------------------------------------------------
// Beat phase, 0 .. 1: a sawtooth, 0 on a beat, rising linearly to 1 just
// before the next. Use it to animate in time WITH the music:
//   1.0 - phase          the fraction of the beat left (anticipation)
//   (1.0 - phase) * 60.0 / bpm   seconds to the next beat
//   fract(phase * 2.0)   eighth notes, fract(phase * 4.0) sixteenths
//   sin(phase * 6.2832)  a swing per beat; exp(-phase * 8.0) a decay per beat
// Don't use the smoothed phase (smoothing blends across the 1 -> 0 wrap).
const float EwAudioBeatPhaseU = 0.25;
const float EwAudioBeatPhaseV = 11.5 / 64.0;
// Beat pulse, 0 .. 1: 1 on a beat of the grid, then * 0.85 per step (half
// after ~4 steps = 70 ms, 0.1 after ~14 steps = 0.24 s). A flash / kick on
// every beat; pow(pulse, k) makes it shorter (k > 1) or longer (k < 1).
const float EwAudioBeatPulseU = 0.75;
const float EwAudioBeatPulseV = 11.5 / 64.0;

// -----------------------------------------------------------------------------
//  Row 12: level | peak | brightness | onset (quarters)
// -----------------------------------------------------------------------------
// Level, 0 .. 1: the loudness, the RMS of the last 2048 samples (~43 ms) in
// dBFS mapped (dB + 60) / 60: 0 = -60 dB or quieter, 1 = 0 dB (full scale; a
// full scale sine is 0.95). Typical mastered music: 0.7 .. 0.85.
const float EwAudioLevelU = 0.125;
const float EwAudioLevelV = 12.5 / 64.0;
// Peak, 0 .. 1: the largest |sample| of the last 2048 samples, LINEAR (not
// dB): 1 = full scale. Reacts faster / jumpier than the level.
const float EwAudioPeakU = 0.375;
const float EwAudioPeakV = 12.5 / 64.0;
// Brightness, 0 .. 1: the spectral centroid (the power weighted mean band) on
// the same log axis as the spectrum: the "center of mass" of the sound,
// frequency = 20 * 1000^brightness Hz (0.5 = 632 Hz). Low: bass heavy, dark;
// high: cymbals, noise, bright. Good for color / hue.
const float EwAudioBrightnessU = 0.625;
const float EwAudioBrightnessV = 12.5 / 64.0;
// Onset, 0 .. 1: how much the spectrum rises now (the spectral flux: the mean
// rise of the 512 bands since the step before, * 10, clamped). High on any new
// note / hit / transient in any band, ~0 on held sounds and silence. The raw
// input of the tempo and beat detection; good for sparks / particle bursts.
const float EwAudioOnsetU = 0.875;
const float EwAudioOnsetV = 12.5 / 64.0;

// -----------------------------------------------------------------------------
//  Row 13: kick pulse | snare pulse | hi-hat pulse | mid energy (quarters)
//  A pulse jumps when its band's energy rises fast (a rise of 1/6 = 12 dB in
//  one step gives 1) and then decays * 0.85 per step (half after 70 ms, 0.1
//  after 0.24 s): pulse = max(min(1, rise * 6), pulse * 0.85). The kick reacts
//  to the bass drum (40 .. 120 Hz), the snare to 1 .. 5 kHz (also vocals,
//  guitars), the hi-hat to 6 .. 16 kHz (cymbals, sibilants).
// -----------------------------------------------------------------------------
const float EwAudioKickPulseU = 0.125;               // kick: jumps on a bass drum hit
const float EwAudioKickPulseV = 13.5 / 64.0;
const float EwAudioSnarePulseU = 0.375;              // snare: jumps on a snare / clap / sharp mid hit
const float EwAudioSnarePulseV = 13.5 / 64.0;
const float EwAudioHiHatPulseU = 0.625;              // hi-hat: jumps on a hi-hat / cymbal hit
const float EwAudioHiHatPulseV = 13.5 / 64.0;
// Mid energy, 0 .. 1: the mean power of 250 Hz .. 2 kHz mapped like the
// spectrum: vocals, guitars, keys, the body of the music.
const float EwAudioMidU = 0.875;
const float EwAudioMidV = 13.5 / 64.0;

// -----------------------------------------------------------------------------
//  Row 14: kick energy | snare energy | hi-hat energy | tempo confidence
//  The energies: the mean power of the band mapped like the spectrum
//  ((dB + 100) / 70), levels (not hits) of the same bands as the pulses.
// -----------------------------------------------------------------------------
const float EwAudioKickU = 0.125;                    // kick energy: 40 .. 120 Hz
const float EwAudioKickV = 14.5 / 64.0;
const float EwAudioSnareU = 0.375;                   // snare energy: 1 .. 5 kHz
const float EwAudioSnareV = 14.5 / 64.0;
const float EwAudioHiHatU = 0.625;                   // hi-hat energy: 6 .. 16 kHz
const float EwAudioHiHatV = 14.5 / 64.0;
// Tempo confidence, 0 .. 1: how strongly the onsets repeat at the tempo's
// period (the normalized autocorrelation). Below ~0.1: no clear beat (ambient,
// speech, the first seconds), above ~0.3: a solid beat. Fade the beat based
// effects in with e.g. smoothstep(0.1, 0.3, confidence).
const float EwAudioTempoConfidenceU = 0.875;
const float EwAudioTempoConfidenceV = 14.5 / 64.0;

// -----------------------------------------------------------------------------
//  Waveform rows 16 .. 25: the last 512 samples (~11 ms at 48 kHz) of the
//  step, oldest (u = 0) .. newest (u = 1). Row 16 + l has 512 >> l segments
//  of 2^l samples, each the sample with the largest amplitude (its sign kept)
//  so peaks survive the down sampling. Stored as 0.5 + 0.5 * sample (0.5 =
//  silence); EwAudioGetWave returns -1 .. 1. Not triggered (no zero crossing
//  sync), so it shifts from step to step like a free running scope.
// -----------------------------------------------------------------------------
const float EwAudioWave512v = 16.5 / 64.0;          // row 16: every sample
const float EwAudioWave256v = 17.5 / 64.0;          // row 17: 256 segments of 2 samples
const float EwAudioWave128v = 18.5 / 64.0;          // row 18: 128 segments of 4 samples
const float EwAudioWave64v = 19.5 / 64.0;           // row 19: 64 segments of 8 samples
const float EwAudioWave32v = 20.5 / 64.0;           // row 20: 32 segments
const float EwAudioWave16v = 21.5 / 64.0;           // row 21: 16 segments
const float EwAudioWave8v = 22.5 / 64.0;            // row 22: 8 segments
const float EwAudioWave4v = 23.5 / 64.0;            // row 23: 4 segments
const float EwAudioWave2v = 24.5 / 64.0;            // row 24: 2 segments
const float EwAudioWave1v = 25.5 / 64.0;            // row 25: 1 segment: the signed peak sample of the 512

// Row 26: the hi-hat energy (as EwAudioHiHatU / V) over the whole row: read it
// at any u (e.g. uv.x), handy with linear filtering
const float EwAudioHiHatRowV = 26.5 / 64.0;

// Rows 15, 27 .. 31: reserved (0)

// Add to a v of rows 0 .. 31: the smoothed value (rows 32 .. 63)
const float EwAudioDiffuseAddV = 0.5;

// The v of a row 0 .. 63
float EwAudio2(int row)
{
    return (float(row) + 0.5) * (1.0 / 64.0);
}

// The u of band / segment i of a row of N (0 .. N - 1)
float EwAudioGet512u(int i) { return (float(i) + 0.5) * (1.0 / 512.0); }
float EwAudioGet256u(int i) { return (float(i) + 0.5) * (1.0 / 256.0); }
float EwAudioGet128u(int i) { return (float(i) + 0.5) * (1.0 / 128.0); }
float EwAudioGet64u(int i) { return (float(i) + 0.5) * (1.0 / 64.0); }
float EwAudioGet32u(int i) { return (float(i) + 0.5) * (1.0 / 32.0); }
float EwAudioGet16u(int i) { return (float(i) + 0.5) * (1.0 / 16.0); }
float EwAudioGet8u(int i) { return (float(i) + 0.5) * (1.0 / 8.0); }
float EwAudioGet4u(int i) { return (float(i) + 0.5) * (1.0 / 4.0); }
float EwAudioGet2u(int i) { return (float(i) + 0.5) * (1.0 / 2.0); }
float EwAudioGet1u(int i) { return 0.5; }

// The u of the band / segment at x (0 .. 1) of a row of N: its center (no blend with the neighbours)
float EwAudioGet512u(float x) { return (floor(clamp(x, 0.0, 1.0) * 511.999) + 0.5) * (1.0 / 512.0); }
float EwAudioGet256u(float x) { return (floor(clamp(x, 0.0, 1.0) * 255.999) + 0.5) * (1.0 / 256.0); }
float EwAudioGet128u(float x) { return (floor(clamp(x, 0.0, 1.0) * 127.999) + 0.5) * (1.0 / 128.0); }
float EwAudioGet64u(float x) { return (floor(clamp(x, 0.0, 1.0) * 63.999) + 0.5) * (1.0 / 64.0); }
float EwAudioGet32u(float x) { return (floor(clamp(x, 0.0, 1.0) * 31.999) + 0.5) * (1.0 / 32.0); }
float EwAudioGet16u(float x) { return (floor(clamp(x, 0.0, 1.0) * 15.999) + 0.5) * (1.0 / 16.0); }
float EwAudioGet8u(float x) { return (floor(clamp(x, 0.0, 1.0) * 7.999) + 0.5) * (1.0 / 8.0); }
float EwAudioGet4u(float x) { return (floor(clamp(x, 0.0, 1.0) * 3.999) + 0.5) * (1.0 / 4.0); }
float EwAudioGet2u(float x) { return (floor(clamp(x, 0.0, 1.0) * 1.999) + 0.5) * (1.0 / 2.0); }
float EwAudioGet1u(float x) { return 0.5; }

// -----------------------------------------------------------------------------
//  The values (the mono mix) of an audio2 texture s (iChannel0 / tex);
//  smoothed = true: the smoothed value (+ EwAudioDiffuseAddV)
// -----------------------------------------------------------------------------
float EwAudioGetValue(sampler2D s, float u, float v, bool smoothed) { return texture(s, vec2(u, v + (smoothed ? EwAudioDiffuseAddV : 0.0))).r; }

// The tempo in BPM (0 = none yet, else 60 .. 200) and how sure it is (0 .. 1, solid above ~0.3)
float EwAudioGetBpm(sampler2D s) { return EwAudioGetValue(s, EwAudioBpmU, EwAudioBpmV, false) * EwAudioBpmScale; }
float EwAudioGetTempoConfidence(sampler2D s) { return EwAudioGetValue(s, EwAudioTempoConfidenceU, EwAudioTempoConfidenceV, false); }
// The beat grid: phase 0 (on a beat) .. 1 (just before the next, never smoothed), pulse 1 on a beat, * 0.85 per step
float EwAudioGetBeatPhase(sampler2D s) { return EwAudioGetValue(s, EwAudioBeatPhaseU, EwAudioBeatPhaseV, false); }
float EwAudioGetBeatPulse(sampler2D s) { return EwAudioGetValue(s, EwAudioBeatPulseU, EwAudioBeatPulseV, false); }
float EwAudioGetBeatPulse(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioBeatPulseU, EwAudioBeatPulseV, smoothed); }

// Band energies 0 .. 1 (the mean power of the band, (dB + 100) / 70): bass 20-150 Hz, mid 250-2000 Hz, kick 40-120 Hz,
// snare 1-5 kHz, hi-hat 6-16 kHz. Levels: high as long as the band is loud
float EwAudioGetBassEnergy(sampler2D s) { return EwAudioGetValue(s, EwAudioBassU, EwAudioBassV, false); }
float EwAudioGetBassEnergy(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioBassU, EwAudioBassV, smoothed); }
float EwAudioGetMidEnergy(sampler2D s) { return EwAudioGetValue(s, EwAudioMidU, EwAudioMidV, false); }
float EwAudioGetMidEnergy(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioMidU, EwAudioMidV, smoothed); }
float EwAudioGetKickEnergy(sampler2D s) { return EwAudioGetValue(s, EwAudioKickU, EwAudioKickV, false); }
float EwAudioGetKickEnergy(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioKickU, EwAudioKickV, smoothed); }
float EwAudioGetSnareEnergy(sampler2D s) { return EwAudioGetValue(s, EwAudioSnareU, EwAudioSnareV, false); }
float EwAudioGetSnareEnergy(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioSnareU, EwAudioSnareV, smoothed); }
float EwAudioGetHiHatEnergy(sampler2D s) { return EwAudioGetValue(s, EwAudioHiHatU, EwAudioHiHatV, false); }
float EwAudioGetHiHatEnergy(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioHiHatU, EwAudioHiHatV, smoothed); }

// Hits 0 .. 1: jump on a fast rise of the kick / snare / hi-hat band, then * 0.85 per step (half after 70 ms)
float EwAudioGetKickPulse(sampler2D s) { return EwAudioGetValue(s, EwAudioKickPulseU, EwAudioKickPulseV, false); }
float EwAudioGetKickPulse(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioKickPulseU, EwAudioKickPulseV, smoothed); }
float EwAudioGetSnarePulse(sampler2D s) { return EwAudioGetValue(s, EwAudioSnarePulseU, EwAudioSnarePulseV, false); }
float EwAudioGetSnarePulse(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioSnarePulseU, EwAudioSnarePulseV, smoothed); }
float EwAudioGetHiHatPulse(sampler2D s) { return EwAudioGetValue(s, EwAudioHiHatPulseU, EwAudioHiHatPulseV, false); }
float EwAudioGetHiHatPulse(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioHiHatPulseU, EwAudioHiHatPulseV, smoothed); }

// Level (RMS, -60 .. 0 dBFS as 0 .. 1), peak (largest |sample|, linear), brightness (spectral centroid on the log axis:
// 20 * 1000^b Hz), onset (spectral flux: how much the spectrum rises now, transients)
float EwAudioGetLevel(sampler2D s) { return EwAudioGetValue(s, EwAudioLevelU, EwAudioLevelV, false); }
float EwAudioGetLevel(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioLevelU, EwAudioLevelV, smoothed); }
float EwAudioGetPeak(sampler2D s) { return EwAudioGetValue(s, EwAudioPeakU, EwAudioPeakV, false); }
float EwAudioGetPeak(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioPeakU, EwAudioPeakV, smoothed); }
float EwAudioGetBrightness(sampler2D s) { return EwAudioGetValue(s, EwAudioBrightnessU, EwAudioBrightnessV, false); }
float EwAudioGetBrightness(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioBrightnessU, EwAudioBrightnessV, smoothed); }
float EwAudioGetOnset(sampler2D s) { return EwAudioGetValue(s, EwAudioOnsetU, EwAudioOnsetV, false); }
float EwAudioGetOnset(sampler2D s, bool smoothed) { return EwAudioGetValue(s, EwAudioOnsetU, EwAudioOnsetV, smoothed); }

// The spectrum (0 .. 1) at x (0 .. 1: 20 Hz .. 20 kHz, log frequency: f = 20 * 1000^x) of a level: 0 = 512 bands,
// 1 = 256 ... 9 = 1 band (the band's center, no blend with the neighbours)
float EwAudioGetFrequency(sampler2D s, float x, int level, bool smoothed)
{
    float n = exp2(9.0 - float(level));
    float u = (floor(clamp(x, 0.0, 1.0) * (n - 0.001)) + 0.5) / n;
    return EwAudioGetValue(s, u, EwAudio2(level), smoothed);
}
float EwAudioGetFrequency(sampler2D s, float x, int level) { return EwAudioGetFrequency(s, x, level, false); }

// The waveform (-1 .. 1) at x (0 .. 1: the last 512 samples, oldest .. newest) of a level: 0 = 512 samples, 1 = 256 ... 9 = 1 segment
float EwAudioGetWave(sampler2D s, float x, int level, bool smoothed)
{
    float n = exp2(9.0 - float(level));
    float u = (floor(clamp(x, 0.0, 1.0) * (n - 0.001)) + 0.5) / n;
    return EwAudioGetValue(s, u, EwAudio2(16 + level), smoothed) * 2.0 - 1.0;
}
float EwAudioGetWave(sampler2D s, float x, int level) { return EwAudioGetWave(s, x, level, false); }
