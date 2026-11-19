# EwAudio2

Audio: the rows (v) and bands (u) of the audio2 texture of an audio2 effect (`//  media: audio2`).

## Signature

```glsl
float EwAudio2(int row)
float EwAudioGet512u(int i)    // also 256, 128, 64, 32, 16, 8, 4, 2, 1
float EwAudioGet512u(float x)  // also 256, 128, 64, 32, 16, 8, 4, 2, 1
// and the row / value constants below

// The values of an audio2 texture s (iChannel0 / tex), the mono mix; most also with (sampler2D s, bool smoothed)
float EwAudioGetBpm(sampler2D s)               // BPM, 0 = none yet
float EwAudioGetTempoConfidence(sampler2D s)   // 0 .. 1
float EwAudioGetBeatPhase(sampler2D s)         // 0 on a beat .. 1
float EwAudioGetBeatPulse(sampler2D s)         // 1 on a beat, decays
float EwAudioGetBassEnergy(sampler2D s)        // also Mid, Kick, Snare, HiHat
float EwAudioGetKickPulse(sampler2D s)         // also Snare, HiHat
float EwAudioGetLevel(sampler2D s)             // also Peak, Brightness, Onset
float EwAudioGetFrequency(sampler2D s, float x, int level)  // the spectrum at x, level 0 = 512 bands .. 9 = 1
float EwAudioGetWave(sampler2D s, float x, int level)       // the waveform at x, -1 .. 1, level 0 = 512 segments .. 9 = 1
float EwAudioGetValue(sampler2D s, float u, float v, bool smoothed)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| row | int | A row of the texture, 0 .. 63. |
| i | int | A band / segment of a row of N values, 0 .. N - 1. |
| x | float | A position 0 .. 1 along a row of N values (the band / segment there). |

## Returns

`EwAudio2(row)`: the v of the row's pixel centers, `(row + 0.5) / 64`. `EwAudioGet<N>u(i)`: the u of the center of band `i`
of a row of N, `(i + 0.5) / N`. `EwAudioGet<N>u(x)`: the u of the center of the band at `x` (so a band reads one value, no
blend with its neighbours).

## Description

An audio2 effect gets its sound (a file, or a live input) as a 512 x 64 RGB float texture on `iChannel0` / `tex`: **r = mono,
g = left, b = right**. It is computed at a fixed 60 analysis steps per second of sound time (the same at any frame rate, when
seeking and in the video export). Rows 0 .. 31 are the values of the moment, rows 32 .. 63 the same values smoothed
(`s += 0.2 * (x - s)` per step: add `EwAudioDiffuseAddV` to a v).

| Rows | Constants (v) | Content |
| :--- | :--- | :--- |
| 0 .. 9 | `EwAudioFrequency512v` .. `EwAudioFrequency1v` | The spectrum on a log frequency axis 20 Hz .. 20 kHz in 512, 256, ... 1 bands (a band of row r spans 2^r pixels, the power average of its bands), -100 .. -30 dB as 0 .. 1 (like ShaderToy). |
| 10 | `EwAudioBassU/V`, `EwAudioBpmU/V` | Bass energy (20 .. 150 Hz, u < 0.5) / the tempo: BPM / 255 (u >= 0.5, `* EwAudioBpmScale` = BPM, 0 = none). |
| 11 | `EwAudioBeatPhaseU/V`, `EwAudioBeatPulseU/V` | The beat phase 0 (on a beat) .. 1 / the beat pulse (1 on a beat, decays). |
| 12 | `EwAudioLevelU/V`, `EwAudioPeakU/V`, `EwAudioBrightnessU/V`, `EwAudioOnsetU/V` | Level (RMS, -60 .. 0 dB as 0 .. 1) / peak amplitude / brightness (the spectral centroid on the log axis, 0 .. 1) / onset (how much the spectrum rises now). |
| 13 | `EwAudioKickPulseU/V`, `EwAudioSnarePulseU/V`, `EwAudioHiHatPulseU/V`, `EwAudioMidU/V` | Kick / snare / hi-hat pulses (jump on a hit, decay) / mid energy (250 Hz .. 2 kHz). |
| 14 | `EwAudioKickU/V`, `EwAudioSnareU/V`, `EwAudioHiHatU/V`, `EwAudioTempoConfidenceU/V` | Kick (40 .. 120 Hz) / snare (1 .. 5 kHz) / hi-hat (6 .. 16 kHz) energy / how sure the tempo is (0 .. 1). |
| 16 .. 25 | `EwAudioWave512v` .. `EwAudioWave1v` | The waveform, the last 512 samples (~12 ms), in 512, 256, ... 1 segments: each the sample with the largest amplitude, its sign kept (0.5 = silence). |
| 26 | `EwAudioHiHatRowV` | The hi-hat energy over the whole row. |
| 15, 27 .. 31 | | Reserved (0). |
| 32 .. 63 | `+ EwAudioDiffuseAddV` | Rows 0 .. 31 smoothed. |

The energies use the same dB mapping as the spectrum. The tempo / beat values come from the mono mix (the same in r, g, b),
estimated 4 times a second from the onsets of the last 6 seconds (60 .. 200 BPM, weighted towards 120 BPM); a still sound or
the first seconds give 0 / a low confidence. A smoothed beat phase / tempo is of little use (it blends across the wrap).

The `EwAudioGet...` functions read r (the mono mix). For stereo read `.g` (left) / `.b` (right) with `texture()` and the same
constants. "A step" below is one analysis step, 1/60 s of sound time; the values change per step, not per frame.

## The values in detail

### Spectrum (rows 0 .. 9)

Row `r` has `512 >> r` bands; a band spans `2^r` pixels, read it at its center (`EwAudioGet<N>u`, or `EwAudioGetFrequency`).

* **x axis (u): log frequency.** x = 0 .. 1 is 20 Hz .. 20 kHz (capped at half the sample rate): `f = 20 * 1000^x` Hz,
  `x = log(f / 20) / log(1000)`. Every octave is ~0.1 wide. 50 Hz = 0.13, 100 Hz = 0.23, 250 Hz = 0.37, 1 kHz = 0.57,
  4 kHz = 0.77, 10 kHz = 0.90.
* **Value: the band's power in dB**, `(dB + 100) / 70` clamped (like ShaderToy): 0 = -100 dB or less, 1 = -30 dB or more.
  It is already logarithmic: use it as a bar height directly; `pow(v, 2.0)` or `smoothstep` adds contrast.
* **The rows:** a coarser row averages the *power* of two bands of the row above (not the mapped values), so a loud narrow
  peak still shows. 512 bands ~0.02 octave each, 64 ~1/6 octave, 32 ~1/3 octave (a 31 band EQ), 8 ~1.25 octaves; the 4 bands
  are 20-112 Hz, 112-632 Hz, 632-3557 Hz, 3.6-20 kHz; 2 bands split at 632 Hz; row 9 is the whole spectrum.
* The FFT has 2048 samples (a bin is ~23 Hz at 48 kHz): the lowest bands of the fine rows are narrower than a bin and are
  interpolated, so they are smooth rather than independent. 64 .. 128 bands suit bar displays best.
* Smoothed (`+ EwAudioDiffuseAddV`): calmer bars; mix raw and smoothed for "punch with a tail".

### Energies (bass, mid, kick, snare, hi-hat)

The mean power of a frequency range, mapped like the spectrum: **levels, not hit detectors** (a held bass note keeps the bass
energy high). Bass 20 .. 150 Hz, mid 250 Hz .. 2 kHz, kick 40 .. 120 Hz, snare 1 .. 5 kHz, hi-hat 6 .. 16 kHz. Row 26 repeats
the hi-hat energy over the whole row (any u). The smoothed bass is a good "pump" for sizes and brightness.

### Pulses (kick, snare, hi-hat)

React to hits: when the band's energy rises fast the pulse jumps, then it decays by `* 0.85` per step:
`pulse = max(min(1, rise * 6), pulse * 0.85)`. A rise of 1/6 (12 dB in one step) gives 1. It is half after ~4 steps (70 ms),
0.1 after ~14 steps (0.24 s). The snare range also catches claps, vocals and guitars, the hi-hat range cymbals and sibilants.
`pow(pulse, k)` makes a flash shorter (k > 1) or longer (k < 1); the smoothed pulse is a soft swell.

### Level, peak, brightness, onset (row 12)

* **Level**: loudness, the RMS of the last 2048 samples (~43 ms) in dBFS, `(dB + 60) / 60`: 0 = -60 dB or quieter, 1 = full
  scale (a full scale sine is 0.95). Mastered music usually sits around 0.7 .. 0.85.
* **Peak**: the largest |sample| of the last 2048 samples, *linear* (1 = full scale). Faster and jumpier than the level.
* **Brightness**: the spectral centroid (the power weighted mean band) on the spectrum's log axis, so the frequency is
  `20 * 1000^brightness` Hz (0.5 = 632 Hz). Low = bass heavy / dark, high = cymbals / noise / bright. Good for color.
* **Onset**: the spectral flux, the mean rise of the 512 bands since the step before, `* 10`, clamped. High on any new
  note, hit or transient in any band, ~0 on held sounds. The input of the tempo / beat detection; good for particle bursts.

### Tempo and beat (rows 10, 11, 14)

* **Tempo**: `EwAudioGetBpm` (or the value `* EwAudioBpmScale`). 0 = none yet (silence, the first seconds, no rhythm), else
  60 .. 200 BPM. Re-estimated 4 times a second (it holds in between) by autocorrelating the onsets of the last 6 seconds,
  weighted towards 120 BPM, so a 70 or 240 BPM song may read as its double / half. The beat period is `60 / bpm` seconds.
* **Tempo confidence**: how strongly the onsets repeat at that period (the normalized autocorrelation), 0 .. 1. Below ~0.1
  there is no clear beat (ambient, speech, the first seconds), above ~0.3 it's solid. Fade beat effects in with
  `smoothstep(0.1, 0.3, confidence)`.
* **The beat grid**: the onsets of the last 6 seconds are combed with the period; the offset that lines up the most onsets
  places the beats. The grid *predicts*: it keeps going through a quiet beat (the kick pulse only *reacts*). Resolution 1 step.
  Without a tempo (0) the grid is a 120 BPM guess, which is why the confidence matters.
* **Beat phase**: a sawtooth, 0 on a beat, rising linearly to 1 just before the next one. Animate *with* the music:
  `1.0 - phase` = the fraction of the beat left (anticipation), `(1.0 - phase) * 60.0 / bpm` = seconds to the next beat,
  `fract(phase * 2.0)` eighths, `fract(phase * 4.0)` sixteenths, `exp(-phase * 8.0)` a decay per beat, `sin(phase * 6.2832)` a
  swing per beat. Never use the smoothed phase (it blends across the 1 -> 0 wrap).
* **Beat pulse**: 1 on each beat of the grid, then `* 0.85` per step (`0.85 ^ steps since the beat`), the same decay as the
  hit pulses.

### Waveform (rows 16 .. 25)

The last 512 samples of the step (~11 ms at 48 kHz), oldest (u = 0) .. newest (u = 1). Row `16 + l` has `512 >> l` segments
of `2^l` samples, each the sample with the largest amplitude with its sign, so peaks survive the down sampling; row 25 is the
signed peak of all 512. Stored as `0.5 + 0.5 * sample` (0.5 = silence); `EwAudioGetWave` returns -1 .. 1. It is not
triggered (no zero crossing sync): it shifts from step to step like a free running scope.

### Smoothed rows (32 .. 63)

Rows 0 .. 31 smoothed with `s += 0.2 * (x - s)` per step: a change is half way after ~3 steps (50 ms) and 90 % after ~10 steps
(170 ms). Add `EwAudioDiffuseAddV` to any v of rows 0 .. 31, or pass `smoothed = true`.

## Requires

None.

## Example

```glsl
//  media: audio2
float bpm = EwAudioGetBpm(iChannel0);
float kick = EwAudioGetKickPulse(iChannel0);
float bass = EwAudioGetBassEnergy(iChannel0, true);      // smoothed
float level = EwAudioGetFrequency(iChannel0, uv.x, 3);   // 64 bands
float wave = EwAudioGetWave(iChannel0, uv.x, 0);         // -1 .. 1
// a flash on every beat, only when the beat is clear
float sure = smoothstep(0.1, 0.3, EwAudioGetTempoConfidence(iChannel0));
float flash = EwAudioGetBeatPulse(iChannel0) * sure;
// or the constants directly
float band = texture(iChannel0, vec2(EwAudioGet64u(uv.x), EwAudioFrequency64v)).r;
float calm = texture(iChannel0, vec2(EwAudioGet64u(uv.x), EwAudioFrequency64v + EwAudioDiffuseAddV)).r;
float left = texture(iChannel0, vec2(EwAudioGet64u(uv.x), EwAudioFrequency64v)).g;   // .g left, .b right
float phase = texture(iChannel0, vec2(EwAudioBeatPhaseU, EwAudioBeatPhaseV)).r;
```
