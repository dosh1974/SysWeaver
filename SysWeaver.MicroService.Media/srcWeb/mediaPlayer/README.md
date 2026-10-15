# mediaPlayer

[⬆ srcWeb](../README.md)

> The JavaScript media player: one player class per media type, plus the WebGL effect program and canvas text tools they share.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Media](../../README.md) |
| **Kind** | Embedded web assets (JavaScript) |

## Purpose

[`media_player.js`](media_player.js) is the entry point. It defines the `MediaTypes` constants (`Image`, `Video`, `Audio`, `YouTube`, `Effect`, `Text`) and the `MediaPlayer` class. `MediaPlayer.Init()` loads `media_player_tools.js` and `canvasTools.js` with `includeJs`, and loads all the other player scripts as well when `window.MediaLoadAll` is set. `MediaPlayer.Create(type, data, mediaParams)` then creates the right player. Each `MediaPlayerParams*` class mirrors one of the C# types in [`Media/`](../../Media/README.md).

Effects are GLSL fragment shaders (see the effect folders under [`mediaView/`](../mediaView/README.md)). [`media_effect_program.js`](media_effect_program.js) compiles them and reads editable parameters from `const` lines that end in a `//var:{...}` comment.

The scripts are served as `mediaPlayer/*.js`. The pages in `mediaView/` include them either directly or through `media_player.js`.

## Contents

| Item | Description |
|---|---|
| [`media_player.js`](media_player.js) | `MediaTypes` constants and the `MediaPlayer` factory/loader |
| [`media_player_tools.js`](media_player_tools.js) | `MediaPlayerTools`: shared helpers for clipping, sizing/alignment, blob caching, unmute detection, page-loaded and parent-window messages, errors |
| [`media_effect_program.js`](media_effect_program.js) | `EffectProgram` / `EffectProgramData`: WebGL shader compilation, linking, and parsing of `//var:` parameter annotations |
| [`media_player_effect.js`](media_player_effect.js) | `MediaPlayerEffect` (renders a GLSL effect, optionally over an image texture) and `MediaPlayerImageTexture` |
| [`media_player_image.js`](media_player_image.js) | `MediaPlayerImage` and its parameters |
| [`media_player_video.js`](media_player_video.js) | `MediaPlayerVideo` and its parameters |
| [`media_player_audio.js`](media_player_audio.js) | `MediaPlayerAudio` and its parameters |
| [`media_player_youtube.js`](media_player_youtube.js) | `MediaPlayerYoutube`: YouTube clip playback |
| [`media_player_text.js`](media_player_text.js) | `MediaPlayerText` / `MediaPlayerTextTexture`: stylised text (single or collage), optionally with an effect |
| [`canvasTools.js`](canvasTools.js) | `TextStyle`, `TextLinearGradient`, `TextGradientStop` and `CanvasTools`: draws styled text to a canvas |

## Relationships

- **Part of:** [srcWeb](../README.md) of [SysWeaver.MicroService.Media](../../README.md)
- **Used by:** the pages in [`mediaView/`](../mediaView/README.md) and its effect sub folders
- **Mirrors:** the C# media types in [`Media/`](../../Media/README.md)
