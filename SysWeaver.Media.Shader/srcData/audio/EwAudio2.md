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
// or the constants directly
float band = texture(iChannel0, vec2(EwAudioGet64u(uv.x), EwAudioFrequency64v)).r;
float calm = texture(iChannel0, vec2(EwAudioGet64u(uv.x), EwAudioFrequency64v + EwAudioDiffuseAddV)).r;
float kick = texture(iChannel0, vec2(EwAudioKickPulseU, EwAudioKickPulseV)).r;
float beat = texture(iChannel0, vec2(EwAudioBeatPhaseU, EwAudioBeatPhaseV)).r;
float bpm = texture(iChannel0, vec2(EwAudioBpmU, EwAudioBpmV)).r * EwAudioBpmScale;
```
