# Media

[⬆ SysWeaver.MicroService.Media](../README.md)

> The C# media description types (image, video, audio, YouTube, Vimeo, effect) that mirror the parameters the browser media player expects.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Media](../README.md) |
| **Kind** | Source code |

## Purpose

These classes live in the `SysWeaver.MicroService.Media` namespace and describe *what* to show and *how*: duration, crop, size, speed, sound, effect parameters and so on. They are plain data types (most implement `IEquatable<T>` and carry `Edit*` attributes such as `[EditRange]`), so they can be edited in the generic UI, stored, and serialized to the web pages in [`srcWeb/`](../srcWeb/README.md), where the JavaScript player classes (`MediaPlayerParamsImage`, `MediaPlayerParamsVideo`, `MediaPlayerParamsEffect`, ...) mirror them.

## Contents

| Item | Description |
|---|---|
| [`IMediaData.cs`](IMediaData.cs) | Base interface for all media data types |
| [`MediaAudio.cs`](MediaAudio.cs) | Audio media data (base class of `MediaVideo`) |
| [`MediaCrop.cs`](MediaCrop.cs) | Crop information used by the media types |
| [`MediaEffect.cs`](MediaEffect.cs) | A GLSL effect to render: reference width/height, speed, offset, size limits, DPI handling, static and transparent options |
| [`MediaImage.cs`](MediaImage.cs) | Meta data for images |
| [`MediaLimits.cs`](MediaLimits.cs) | Constants such as `MaxImageDuration` (5 minutes) |
| [`MediaTypes.cs`](MediaTypes.cs) | Enum of media kinds: `Image`, `Video`, `Audio`, `YouTube`, `Effect` |
| [`MediaVideo.cs`](MediaVideo.cs) | Video media data (derives from `MediaAudio`) |
| [`MediaVideoStreams.cs`](MediaVideoStreams.cs) | Choose to use only the audio or only the video stream of a media file |
| [`MediaVimeo.cs`](MediaVimeo.cs) | Meta data for Vimeo hosted videos |
| [`MediaYouTube.cs`](MediaYouTube.cs) | Meta data for YouTube hosted videos |

## Limitations and considerations

- The JavaScript `MediaTypes` class in [`srcWeb/mediaPlayer/media_player.js`](../srcWeb/mediaPlayer/media_player.js) also defines a `Text` media type (5), which has no counterpart in the C# enum.

## Relationships

- **Part of:** [SysWeaver.MicroService.Media](../README.md)
- **Used by:** [SysWeaver.MicroService.Thumbnail.Web](../../SysWeaver.MicroService.Thumbnail.Web/README.md) (media description types), the player scripts in [`srcWeb/mediaPlayer/`](../srcWeb/mediaPlayer/README.md)
