# EwAudio2

Audio: the rows (v), bands and slots (u) of the audio texture of an audio effect (`//  media: audio`; the kind
`audio2` is also read). `legacyaudio` is ShaderToy's 512 x 2 texture, another layout.

## Signature

```glsl
float EwAudio2(int row)
float EwAudioGet512u(int i)    // also 256, 128, 64, 32, 16, 8, 4, 2, 1
float EwAudioGet512u(float x)  // also 256, 128, 64, 32, 16, 8, 4, 2, 1
// and the row / value constants below

// The values of an audio texture s (iChannel0 / tex), the mix; many also with (sampler2D s, bool smoothed)
float EwAudioGetFrequency(sampler2D s, float x, int level)  // the spectrum at x (log axis), level 0 = 512 bands .. 9 = 1
float EwAudioGetWave(sampler2D s, float x, int level)       // the waveform at x, -1 .. 1
float EwAudioGetBassEnergy(sampler2D s)        // also Mid, Sub, Kick, Snare, HiHat
float EwAudioGetLevel(sampler2D s)             // momentary loudness; EwAudioGetLoudness(s, true): 3 s
float EwAudioGetPeak(sampler2D s)              // also Brightness, Onset, Width
float EwAudioGetRelative(sampler2D s, int band)              // auto gain: EwAudioBandBass / Mid / High / Loudness
float EwAudioGetKickPulse(sampler2D s)         // also Snare, HiHat
float EwAudioGetSince(sampler2D s, int hit)    // seconds since EwAudioHitKick / Snare / HiHat (2 = none)
float EwAudioGetHitStrength(sampler2D s, int hit)
float EwAudioGetHit(sampler2D s, int hit, float rate)        // strength * exp(-since * rate)
float EwAudioGetBpm(sampler2D s)               // BPM, 0 = none yet
float EwAudioGetTempoConfidence(sampler2D s)   // 0 .. 1
float EwAudioGetBeatPhase(sampler2D s)         // 0 on a beat .. 1; (s, float latency): seconds ahead
float EwAudioGetBeatPulse(sampler2D s)         // 1 on a beat, decays; (s, float latency)
float EwAudioGetBarPhase(sampler2D s)          // 0 on a downbeat .. 1; (s, float latency)
float EwAudioGetDownbeatPulse(sampler2D s)     // (s, float latency)
float EwAudioGetBeatInBar(sampler2D s)         // 0 .. 3
float EwAudioGetGridKickPulse(sampler2D s)     // the kick pulse near a beat only
float EwAudioGetBarConfidence(sampler2D s)
float EwAudioGetIntensity(sampler2D s)         // also KickPresence, BuildUp, Drop, LoudnessRange
float EwAudioGetChroma(sampler2D s, int pitchClass)          // 0 = C .. 11 = B
float EwAudioGetTonalness(sampler2D s)         // also ChromaHue, ChromaHueStrength
float EwAudioGetTravel(sampler2D s, int band)  // seconds
float EwAudioGetTravelTime(sampler2D s, int band, float time, float gain)
vec3  EwAudioGetHistory(sampler2D s, float x)  // low / mid / high envelopes, x 0 oldest .. 1 newest
float EwAudioGetValue(sampler2D s, float u, float v, bool smoothed)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| row | int | A row of the texture, 0 .. 63. |
| i | int | A band / segment of a row of N values, 0 .. N - 1. |
| x | float | A position 0 .. 1 along a row (the band / segment there; the history: the time). |
| level | int | A spectrum / waveform row: 0 = 512 .. 9 = 1 values. |
| band | int | `EwAudioBandBass` (0), `EwAudioBandMid`, `EwAudioBandHigh`, `EwAudioBandLoudness` (3); the history: 0 .. 2. |
| hit | int | `EwAudioHitKick` (0), `EwAudioHitSnare`, `EwAudioHitHiHat` (2). |
| latency | float | Seconds the picture is shown late (LED walls, projectors: 0.03 .. 0.06): the phase / pulse that far ahead. |

## Returns

`EwAudio2(row)`: the v of the row's pixel centers, `(row + 0.5) / 64`. `EwAudioGet<N>u(i)`: the u of the center of band `i`
of a row of N. The values: see Description.

## Description

An audio effect gets its sound (a file, or a live input) as a 512 x 64 RGB float texture on `iChannel0` / `tex`: **r = the mix,
g = left, b = right**; the rows marked "mix" hold the mix's value in r, g and b. It is computed at a fixed 60 analysis steps per
second of sound time (the same at any frame rate, when seeking and in the video export: SysWeaver's MediaAudio2Engine, Effect
Weaver's AudioTrack2.cs). Rows 0 .. 31 are the values of the moment, rows 32 .. 63 the same smoothed (attack 0.5, release 0.1 per
step: add `EwAudioDiffuseAddV` to a v, or pass `smoothed = true`); phases, positions, the tempo, the hue, "since", travel and the
history are copied unsmoothed.

| Rows | Constants | Content |
| :--- | :--- | :--- |
| 0 .. 9 | `EwAudioFrequency512v` .. `EwAudioFrequency1v` | The spectrum, log frequency 20 Hz .. 20 kHz, 512 .. 1 bands. |
| 10 | `EwAudioBassU/V`, `EwAudioBpmU/V` | Bass energy (30 .. 250 Hz) / the tempo: BPM / 255 (mix). |
| 11 | `EwAudioBeatPhaseU/V`, `EwAudioBeatPulseU/V` | Beat phase / beat pulse (mix). |
| 12 | `EwAudioLevelU/V`, `EwAudioPeakU/V`, `EwAudioBrightnessU/V`, `EwAudioOnsetU/V` | Loudness (400 ms) / peak / brightness / onset (mix). |
| 13 | `EwAudioKickPulseU/V`, `EwAudioSnarePulseU/V`, `EwAudioHiHatPulseU/V`, `EwAudioMidU/V` | Hit pulses / mid energy. |
| 14 | `EwAudioKickU/V`, `EwAudioSnareU/V`, `EwAudioHiHatU/V`, `EwAudioTempoConfidenceU/V` | Kick / snare / hi-hat energy / tempo confidence (mix). |
| 15 | `EwAudioSongV` + `EwAudioShortTermLoudnessU`, `WidthU`, `SubU`, `IntensityU`, `KickPresenceU`, `BuildUpU`, `DropU`, `LoudnessRangeU` | The song (mix, eighths). |
| 16 .. 25 | `EwAudioWave512v` .. `EwAudioWave1v` | The waveform, the last 512 samples, 512 .. 1 segments. |
| 26 | `EwAudioHistoryV` | The last 512 steps (8.5 s): r low, g mid, b high band envelopes. |
| 27 | `EwAudioRelativeV` | Relative bass / mid / high / loudness (mix, quarters). |
| 28 | `EwAudioBarV` + `EwAudioBarPhaseU`, `DownbeatPulseU`, `BeatInBarU`, `GridKickU` | The bar (mix). |
| 29 | `EwAudioChromaV` + `EwAudioTonalnessU`, `ChromaHueU`, `ChromaHueStrengthU` | Chroma C .. B (s1 .. s12), tonalness, hue (mix, sixteenths). |
| 30 | `EwAudioHitsV` + `EwAudioBarConfidenceU` | Since / strength of the last kick, snare, hi-hat; the bar confidence (mix, eighths). |
| 31 | `EwAudioTravelV` | Travel of bass / mid / high / loudness (mix, quarters, seconds). |

## The values in detail

### Spectrum (rows 0 .. 9)

* **x axis (u): log frequency.** x = 0 .. 1 is 20 Hz .. 20 kHz (capped at half the sample rate): `f = 20 * 1000^x` Hz,
  `x = log(f / 20) / log(1000)`. Every octave is ~0.1 wide. 50 Hz = 0.13, 100 Hz = 0.23, 250 Hz = 0.37, 1 kHz = 0.57,
  4 kHz = 0.77, 10 kHz = 0.90.
* **Value:** the band's power (the sum of its FFT bins, Parseval scaled) per 1/3 octave, +1.5 dB per octave around 1 kHz, so pink
  noise and typical music read flat; `(dB + 74) / 58` clamped. Loud masters peak around 0.85 .. 0.95: the bass doesn't stick at 1,
  the highs aren't dead. `pow(v, 2.0)` or `smoothstep` adds contrast.
* **Resolution:** an 8192 sample FFT below 300 Hz (~6 Hz bins: separate bass notes), crossfaded into a 2048 sample one by 600 Hz;
  the DC is removed. A coarser row averages the power of 2 bands of the row above. 64 .. 128 bands suit bar displays best.

### Energies and loudness

* **Energies** (levels, not hits; mapped like the spectrum): bass 30 .. 250 Hz, mid 250 Hz .. 2 kHz, sub 25 .. 55 Hz, kick 45 ..
  110 Hz, snare 1.5 .. 6 kHz, hi-hat 8 .. 16 kHz.
* **Loudness** ("level"): K-weighted like LUFS meters (ITU-R BS.1770; r = left + right power), momentary (400 ms, row 12) and
  short-term (3 s, row 15), `(LUFS + 45) / 40`: mastered music ~0.75 .. 0.85. **Peak**: the largest |sample| of the last 2048,
  linear. **Brightness**: the spectral centroid on the log axis (`20 * 1000^b` Hz; balanced music ~0.5). **Onset**: how much the
  sound rises now (kick / snare / hi-hat rises and the spectral flux). **Width**: side / (mid + side) power above 200 Hz (0 mono,
  0.5 wide, 1 out of phase).
* **Relative** (auto gain, row 27): bass / mid / high (4 .. 16 kHz) / loudness between their 10th and 95th percentile of the last
  8 s (a span of at least 12 dB, 6 for the loudness): 0 .. 1 on any track and input level; a quiet breakdown stays low.

### Hits (rows 13, 30)

Found in the time domain: a band's 10 .. 20 ms envelope rising (kick 45 .. 110 Hz by 3 dB; snare: the 2 .. 5 kHz crack by 2 dB with
its 150 .. 300 Hz body; hi-hat above 8 kHz by 1.5 dB) and standing 4 dB out of the last 0.5 s; one hit per 100 ms (hi-hat 50 ms).
**Pulse**: jumps to the hit's strength, then `* 0.85` per step (half after 70 ms, 0.1 after 0.24 s). **Since** (row 30): seconds / 2
since the last hit (1 = 2 s or more), **strength**: that hit's. `EwAudioGetHit(s, hit, rate)` = `strength * exp(-seconds * rate)`:
any decay; since is also a ramp (a ring's radius). The **grid kick** (row 28) is the kick pulse only near a beat: stable flashes.

### Tempo, beat and bar (rows 10, 11, 14, 28, 30)

* **Tempo**: 60 .. 200 BPM (0 = none yet), from the onsets of the last 6 s (kick weighted), 4 times a second; it changes when another
  tempo wins clearly (a half / double one by 40 %, never while the kick is away). **Confidence**: below ~0.15 no clear beat, above
  ~0.35 solid: `smoothstep(0.15, 0.35, c)`.
* **Beat phase**: a sawtooth, 0 on a beat .. 1, from a phase locked loop: it runs smoothly and keeps going through a quiet beat (it
  predicts). `fract(phase * 2.0)` eighths, `exp(-phase * 8.0)` a decay per beat, `sin(phase * 6.2832)` a swing. Never smoothed.
  **Beat pulse**: `0.85 ^ steps since the beat`.
* **Bar**: the downbeat is where the kick, a crash and a loudness rise land and the snare doesn't (beats 2 and 4); it keeps counting
  1 2 3 4 and moves only when another position wins clearly. **Bar phase** `(beat in bar + beat phase) / 4`, **downbeat pulse**,
  **beat in bar** (`EwAudioGetBeatInBar`: 0 .. 3). **Bar confidence**: gate bar effects with `smoothstep(0.3, 0.6, c)` (tracks
  without a clear kick / snare pattern give a guess).
* **Latency**: `(s, float latency)` overloads of the beat / bar phase and pulse look that many seconds ahead (a display that shows
  the picture late).

### The song (row 15)

**Intensity** (a slow "hype" 0 .. 1: the loudness within the last 16 s, the kick presence, the hit rate; attack 0.5 s, release 3 s),
**kick presence** (0 in a breakdown / intro, 1 with the kick; the kick band against the loudness, level independent), **build-up**
(rising snare / hi-hat rolls and the hi-hat band over 8 s while the kick is away; 0 for 4 bars after a drop), **drop** (1 on the
first strong kick after 6 of 8 s without the kick, linear to 0 over a bar), **loudness range** (the 3 s loudness within its range of
the last 16 s).

### Chroma (row 29)

The tonal peaks of 220 Hz .. 4 kHz by pitch class (C = 0 .. B = 11), max = 1, times the **tonalness** (0 noise / drums .. 1 tones,
from the spectral flatness). **Hue**: the circular mean of the chroma of the last 0.5 s, 0 .. 1 around the color wheel (C = 0): a
color for the key / chord (not smoothed: it wraps); **hue strength** how clear it is (often 0.05 .. 0.3).

### Travel (row 31) and history (row 26)

* **Travel**: `Σ relative value * (K - age) / K / 60` over the last 2 s, seconds (0 .. ~1). It changes by
  `(value - its recent mean) / 60` per step, so `time + gain * travel` (`EwAudioGetTravelTime`, gain <= 1) runs faster while the
  band is above its recent mean and never backwards: scrolling / rotation that surges with the music. `time` = the sound's time.
* **History**: the last 512 steps (8.5 s), oldest (u = 0) .. newest (u = 1): r = the kick band, g = the snare crack band, b = the
  hi-hat band envelopes, `(dB + 60) / 50`. Waterfalls, trails, ribbons (the row moves 1/512 per step).

### Waveform (rows 16 .. 25)

The last 512 samples of the step (~11 ms at 48 kHz), oldest (u = 0) .. newest (u = 1). Row `16 + l` has `512 >> l` segments of
`2^l` samples, each the sample with the largest amplitude with its sign. Stored as `0.5 + 0.5 * sample`; `EwAudioGetWave` returns
-1 .. 1. Free running (not triggered).

## Requires

None.

## Example

```glsl
//  media: audio
float kick = EwAudioGetKickPulse(iChannel0);
float bass = EwAudioGetRelative(iChannel0, EwAudioBandBass, true);   // auto gain, smoothed
float level = EwAudioGetFrequency(iChannel0, uv.x, 3);              // 64 bands
float wave = EwAudioGetWave(iChannel0, uv.x, 0);                    // -1 .. 1
// a flash on every beat, only when the beat is clear
float sure = smoothstep(0.15, 0.35, EwAudioGetTempoConfidence(iChannel0));
float flash = EwAudioGetBeatPulse(iChannel0) * sure;
// a ring per kick, growing for 0.5 s
float ring = smoothstep(0.02, 0.0, abs(length(uv - 0.5) - EwAudioGetSince(iChannel0, EwAudioHitKick)));
// scene change on the drop, motion that surges with the bass
float drop = EwAudioGetDrop(iChannel0);
float t = EwAudioGetTravelTime(iChannel0, EwAudioBandBass, iTime, 0.8);
```
