# audioEffects

[⬆ mediaView](../README.md)

> GLSL effects that draw a sound: spectrum analyzers, VU meters and music driven scenes.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Media](../../../README.md) |
| **Kind** | Embedded web assets (GLSL shaders) |

## Purpose

An audio effect is the picture a sound plays with in the media viewer, the media editor and the preview. The shaders read the sound from the audio texture on `iChannel0` (all of them: their media line says `audio`): the 512 x 64 texture of [`../../mediaPlayer/media_audio2.js`](../../mediaPlayer/media_audio2.js) (a calibrated log spectrum, waveform, kick / snare / hi-hat hits, loudness, tempo, beat and bar, drops, auto gain ...; read with the snippet EwAudio2). An effect without a media line, or with `legacyaudio`, gets ShaderToy's 512 x 2 texture (spectrum and waveform). Each of them describes its four live controls (`//  iMouse.x = Name | Description | Default | Min .. Max | Unit`), which the controls panel of the viewer shows as sliders.

The `MediaService.GetAudioEffects` API lists the effects by file name without `.glsl`, so a new shader dropped in here shows up automatically. The viewer lists them under Visualization in its menu, and the media editor in its effect picker. A sound whose settings name no effect plays with `DefaultAudioEffect` of [`../media.js`](../media.js) ([`Corridor.glsl`](Corridor.glsl)).

A media line (`//  media: audio stock:file.flac`) names the sound an effect plays with when it is shown on its own; references that aren't urls (ex: Effect Weaver's `stock:` media) are resolved by `MediaService.ResolveEffectMedia`.

## Contents

| Item | Description |
|---|---|
| [`Audio.glsl`](Audio.glsl) | Concentric rings driven by the spectrum, crossed by a glowing diagonal X |
| [`Copper_Spectrum.glsl`](Copper_Spectrum.glsl) | Polished copper rods on a glossy tile floor |
| [`Corridor.glsl`](Corridor.glsl) | A hazy corridor with LED spectrum walls for the left and right channel and a glossy floor (the default) |
| [`HSV_Spectrum_Bars.glsl`](HSV_Spectrum_Bars.glsl) | Log frequency bars in a perceptual (Oklab) color gradient |
| [`Hifi_VU_Meters.glsl`](Hifi_VU_Meters.glsl) | Two backlit analog VU meters on a vintage amplifier front |
| [`Led_Spectrum.glsl`](Led_Spectrum.glsl) | A classic LED matrix spectrum analyzer |
| [`Spectrum.glsl`](Spectrum.glsl) | A mirrored spectrum analyzer with a waveform line |
| [`Spectrum_Neon_Grid.glsl`](Spectrum_Neon_Grid.glsl) | Neon grid lines bending with the spectrum |
| [`Spectrum_Pillar_Hall.glsl`](Spectrum_Pillar_Hall.glsl) | A neon pillar hall, one pillar per spectrum band |
| [`Vice_Sunset.glsl`](Vice_Sunset.glsl) | An 80's neon sunset over the ocean with a music driven skyline |

## Limitations and considerations

- There is no `index.html` and no gallery page for this folder (the other effect folders have both); the effects are chosen in the viewer and the media editor.

## Relationships

- **Part of:** [mediaView](../README.md) of [SysWeaver.MicroService.Media](../../../README.md)
- **Uses:** [`mediaPlayer/`](../../mediaPlayer/README.md) (`media_player_effect.js`, `media_player_audio.js`, `media_audio2.js`)
- **Used by:** [`../media.js`](../media.js), [`../mediaEdit.js`](../mediaEdit.js)
