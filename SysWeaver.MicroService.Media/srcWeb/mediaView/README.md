# mediaView

[⬆ srcWeb](../README.md)

> The media viewer pages: the extension viewer, the preview / screenshot page, the map view, the effect galleries and demo pages, and the GLSL effect folders.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Media](../../README.md) |
| **Kind** | Embedded web assets |

## Purpose

`MediaView.html` is the server's extension viewer for media files (`MediaView.html?link={0}`, MediaService). `MediaPreview.html` is a bare full window player; the thumbnail services (Thumbnail.Web, MediaThumbnail) load it headless and talk to it through the `ScreenShotHost` host object. `MapView.html` renders Google Maps / OpenStreetMap views (Thumbnail.Web `GetGoogleMap`). The `*EffectsLib.html`, `TextDemo.html` and `MapDemo.html` pages are developer galleries of the Debug/Media menu. The effect folders hold the `.glsl` effects that `mediaView/Api/Get*Effects` enumerates, each with a lean `index.html` that renders one effect full window.

All players come from [`../mediaPlayer`](../mediaPlayer/README.md).

## Contents

| Item | Description |
|---|---|
| [`MediaView.html`](MediaView.html) + [`mediaViewPage.js`](mediaViewPage.js) | Viewer page (new format: `sw/core.js`, `sw/ui.js`); boots `Sw.MediaView.ViewMain` with the loading overlay. |
| [`MediaPreview.html`](MediaPreview.html) + [`mediaPreviewPage.js`](mediaPreviewPage.js) | Bare player / screenshot page (new format, transparent, no loader); boots `Sw.MediaView.PreviewMain`. |
| [`MediaEdit.html`](MediaEdit.html) + [`mediaEditPage.js`](mediaEditPage.js) | Media editor page (new format); boots `Sw.MediaEdit.Main`. |
| [`mediaEdit.js`](mediaEdit.js), [`mediaEdit.css`](mediaEdit.css) | `Sw.MediaEdit`: the media editor (open any media, the player, the settings in a Sw.PropertyEditor, pop out, share). |
| [`media.js`](media.js) | `Sw.MediaView`: player chrome (preview box, stats, controls), playback preferences, viewer, preview, the media editor (MediaEdit.html, also callable by host pages); legacy globals `mediaCreatePlayer`, `mediaPlayWithPopup`, `mediaEditProps`, `mediaEditMain`, `mediaGetTypeFromUrl`, `mediaViewMain`, `mediaPreviewMain`, `SsHack`. |
| [`media.css`](media.css) | Styles of the custom elements of media.js and the player button icons. |
| [`MapView.html`](MapView.html) + [`mapView.js`](mapView.js) | Map view (lean: only `../core/i18n.js`). Online, with the same requests as the original page: the map frame (OpenStreetMap, or Google with a key) is created at once; a Google type without a `key` url parameter asks `mediaView/Api/GetGoogleMapsKey` (else OpenStreetMap is used, with a console warning). |
| [`MapDemo.html`](MapDemo.html) + [`mapDemo.js`](mapDemo.js) | Gallery of MapView embeds. |
| [`TextDemo.html`](TextDemo.html) + [`textDemo.js`](textDemo.js) | Text style presets with links to `textEffects/`. |
| [`demo.css`](demo.css) | Styles of TextDemo and MapDemo. |
| [`BackgroundEffectsLib.html`](BackgroundEffectsLib.html), [`ImageEffectsLib.html`](ImageEffectsLib.html), [`CollageEffectsLib.html`](CollageEffectsLib.html) + [`effectsLib.js`](effectsLib.js), [`effectsLib.css`](effectsLib.css) | Effect galleries (thumbnails rendered in the browser). CollageEffectsLib.html also documents the collage effect parameters in a comment. |
| [`effectPage.js`](effectPage.js) | Shared logic of the effect folder `index.html` pages. |
| [`parseTools.js`](parseTools.js) | `ParseTools`: url parameters / json side files / objects to typed params. |
| [`textEffect.js`](textEffect.js), [`textEffect.css`](textEffect.css) | `TextEffect` (text and counter effect factories), `CounterEffectParams`, the counter demo boot `textEffectMain`. |
| [`DemoCollage.svg`](DemoCollage.svg), [`DemoCollage.json`](DemoCollage.json) | Demo texture atlas for the collage effects and its atlas variables. |
| [`backgroundEffects/`](backgroundEffects/README.md), [`imageEffects/`](imageEffects/README.md), [`collageEffects/`](collageEffects/README.md), [`counterEffects/`](counterEffects/README.md), [`textEffects/`](textEffects/README.md) | Effect folders (`.glsl` files are enumerated by the server; keep names and folders). |
| [`map/`](map/README.md) | Pin images of MapView. |

## Relationships

- **Part of:** [SysWeaver.MicroService.Media](../../README.md)
- **Uses:** [`../mediaPlayer`](../mediaPlayer/README.md), the SysWeaver core / ui (`../sw/core.js`, `../sw/ui.js`, `../common/theme.css`, `../sw/ui.css`) on the new-format pages, `../common/anim.js` (effect smoothing).
- **Server contract:** `MediaView.html?link=` (raw), `MediaPreview.html?type=&link=&pos=&props=&fill`, `MapView.html?key=&center=&zoom=&language=&maptype=&dpi=&pin=&pinX=&pinY=&pinHeight=`, `mediaView/Api/GetBackgroundEffects|GetImageEffects|GetCollageEffects|GetCounterEffects|GetGoogleMapsKey`, host objects `ScreenShotHost` (`isSupported`, `setSize`, `doIt`) and `ErrorText.SetText`.

## Behavior

### Page formats

- **New format** (core + ui): MediaView, MediaPreview, the three galleries, TextDemo, MapDemo. They follow the page protocol (`Sw.Page.Main`, loader, `Sw.Page.Loaded`), use Sw components (buttons, toasts, clipboard, hints) and translate their texts.
- **Lean pages** (no core): MapView (only `../core/i18n.js` for the translation fallbacks), the effect folder `index.html` pages and `counterEffects/index.html`. They are embedded many times, used as transparent backgrounds and captured headless, so they avoid the session / InterOp start up of the core. They still post the page protocol by hand (`PageLoaded` to the parent, `LoaderRemoved` to the top window), so the app shell reveals them at once.

### MediaView.html

Url (case insensitive keys): `link` | `d` (required), `type` | `t` (default from the extension: image / video / audio extensions, `.glsl` = effect; a YouTube link is a YouTube media), `props` (JSON of the type's params), `no_stats`, `no_controls`, `muted`, `loop` (start muted / looping instead of the visitor's stored preferences, which they don't change; ex: a page background), `pos` (seconds to start at), `ctrl` (InterOp prefix of a linked editor).
Security (`MediaLinkProblem`, also in MediaPreview.html): `link` comes from the url. For the url types (image, video, audio, effect) a http(s) url of this site is always shown, any other scheme (`javascript:`, `data:`, `blob:`, ...) is refused, and a url of another site is refused inside the app shell (it would look like content of this site): the page shows the reason and an "Open in a new tab" button instead (announced to screen readers). In a window of its own (a new tab, the editor's "New window", the screenshot host) or framed by another host, external media is shown as before. YouTube codes and text are not urls and are not checked. The same rule covers the url-like members of `props`: `Effect` and `FontUrl` (another site inside the app shell: "Open in a new tab" opens this page in a new tab). A text media (type 5) from the url is limited inside the app shell to 80 characters (200 for a collage) and, in the shell's content frame (MediaView and MediaPreview; MediaPreview also with the `caption` parameter), captioned "Text from the link" (`p.MediaTextCaption`, text only, body text size from the tokens, so it grows with `--sw-scale`). The caption is placed directly above the rendered text (`PlaceTextCaption`, below it when there is no room) and kept there on resize. MediaPreview then fits the text into a limited area in the middle (`body.MediaCaptioned`: 70% x 35% of the window, a collage 60% x 45%), so a crafted link can't fill the page. The caption depends only on the text coming from the url: other url parameters (the debug `hack` host, unknown ones) never remove it; only a real screenshot host (headless capture, never inside the app) skips it. A web font that fails no longer blanks the page: the text is rendered with a default font (`MediaPlayerTools.LoadFontOrFallback` appends "Verdana, sans-serif") and a small persistent notice "The font could not be loaded, a default font is used." stays at the bottom left (`FontNotice`, `p.MediaFontNotice`, announced; MediaView and MediaPreview, so also the text / collage effect pages); a real screenshot host still gets the failure (`doIt(0, 0, message, 0)`). `FontUrl` is loaded with `MediaPlayerTools.LoadFont` (parsed and escaped, never pasted into CSS). A media that fails to load (missing file, unsupported, more than 30 s) shows an error state with the reason and "Try again" (it used to spin forever: a failed video / audio `<source>` fires `error` only on the source element). `ParseTools.MessageProxy` and the effect pages only relay / accept messages of this origin.
**Player (`CreateChrome`, the default):** a media player like a video player. The media fills a black stage (`Media-View.MediaChrome`, automatic aspect, centered) and the controls are on top of it: a title bar with the file name, a bottom bar with the time line, Play / Pause, Stop, Loop (a switch), the volume (Mute + a slider, with audio), the time (`1:23 / 4:56`), Stats, Open file and Full screen, a big Play button while paused, and the stats panel (top right, the stat chips as a list, opened with Stats or `I`, remembered). The bars and the mouse pointer hide after 2.5 s without activity while the media plays; they stay while paused, while the pointer is on them, while a control has the keyboard focus, while dragging, and for audio only media (a speaker in the middle). Mouse: a click on the media plays / pauses, a double click toggles full screen; touch: a tap shows / hides the controls. Keys (the player is focusable, a region "Media player"): Space / K play / pause, Left / Right -5 / +5 s, Home the start, Up / Down the volume, M mute, F full screen, I stats. A media without a known end (an effect, `Duration <= 0`) has a time line that grows in steps of 5 minutes (5:00, 10:00, ...) and shows `∞` as its length; seeking works on it (dragging the time line seeks live, one seek in flight). Full screen is the Fullscreen api on the player; when the browser can't (ex: an iPhone) it opens `MediaPreview.html?full&back&pos=<position>&type&link&props` in the same frame as before. Narrow windows / phones: no volume slider (the device keys) and no Stop. With `no_controls` there is no chrome: the media and the stats as before (`no_stats` hides the Stats button and panel).
**YouTube links** (`ParseYouTube`, also MediaPreview): `link` may be a YouTube link (youtube.com/watch?v=, youtu.be/, /embed/, /shorts/, /live/, m. / music. / youtube-nocookie.com), with or without `type=3`: it becomes the video code, its `t=` / `start=` (90, 90s, 1m30s) the start position (a seek, not `StartAt`). The title is "YouTube <code>".
**Menu** (⋮ in the bar, also the context menu of the media): Play / Pause, Stop, Back / Forward 15 seconds (J / L), Loop, Speed (25, 50, 75, 100, 125, 150, 200, 1000 %, the speeds the player has: `SetRate` / `MaxRate`; YouTube up to 200 %; images and texts have none; kept for a rebuilt player), Mute, Show the statistics, Full screen, **Show url...** (U: a dialog that takes a media address), **Copy information to clipboard** (name, address, type, page, state, position, duration, loop, speed, mute, volume, every shown stat chip and the settings json), Open the original file. **Double click / tap** on the left / right third: -15 / +15 s ("-15 s" shows on that side; more taps in a row jump again), in the middle: full screen; a single click still plays / pauses after 300 ms. **Drop or paste a link** (a dragged link or text; Ctrl+V while the page has the focus and no text box is focused): a media link (`ParseMediaLink`: a YouTube link or the address of a file of a known type) is shown (`ShowLink`: this page with the new `link` and `type`, other view options kept, `props` / `ctrl` dropped, so the same link checks apply), anything else gets a warning toast. While a link is dragged over the player it has an accent outline (`MediaDropping`).
A still image (the image player) has the same controls like any other media, as before the rewrite (its time line times slideshows). The previous row of buttons (`CreateControls`: Pause, Play, Stop, Loop, Once, Mute, Un mute, VOL 100/75/50/25 %, Open file) is still what `CreatePlayer` adds for other callers (MediaEdit.html, host pages). Stats chips (at least 13 px, 0.9 times the page text size `--sw-size`, growing with `--sw-scale` on large screens; sans serif with tabular numbers) copy `name: text (value)` on click / Enter / Space; their descriptions show as tool tip, on long press and keyboard focus. Open file (`external` icon) opens the original file in a new tab, or its registered editor (`Sw.Page.EditOrOpen`).
Playback starts with the stored preferences; when the browser blocks audio and the user did not choose mute, a full page "Tap or click once to get audio!" button appears (tap, click, Enter, Space). Tool tips are device neutral ("Pause media", "Show the media in full screen", not "Click to ...").
With `ctrl`: `<ctrl>GetProps` is posted and a `<ctrl>SetProps` answer within 100 ms replaces the props; `<ctrl>Reload` brings the complete media props (the rebuild keeps volume, mute, loop, play state and position; the media props are kept apart from the player's params, so an image or a text with an effect rebuilds right), `<ctrl>Load` {Link, MediaType} opens another media (still linked), other `<ctrl>*` messages copy matching fields into the player's params and handle SetVolume, UpdateSize, Crop and Compile; leaving posts `<ctrl>Closed`.

### MediaPreview.html (screenshot contract)

Url: `link` | `d`, `type` | `t`, `props`, `pos` (seconds), `fill`, `full` (full screen, re-entered on the first gesture), `back` (click, Escape or Backspace goes back once the first gesture needs are consumed), `hack` (logging host; it never removes the "Text from the link" caption), `transparent` (ignored), `ShaderDebug=true`. Without a real host a failure shows the error state with the reason and "Try again" (`ShowLoadError`, was only a toast over an empty page).
Host: `window.ScreenShotHost` (after `CefSharp.BindObjectAsync()`), else `chrome.webview.hostObjects.ScreenShotHost`, else (with `hack`) `SsHack`. With a host: `await isSupported()` first, fill mode, class `NoFade` (checkerboard, no transitions), `setSize(w, h)` (2 arguments, as before) to the clipped media size (or the aspect with AdaptiveSize) while waiting up to 1 s for the resize, play, show, measure 600 ms, pause, seek back to `pos`, then `doIt(duration, fps, null, compilationTime)`. Any failure (also a missing parameter or a media that can't be loaded, which used to end without a report) calls `doIt(0, 0, message, 0)` once, so the host does not wait for its time out.
The player answers the postMessage commands MediaPlay, MediaPause, MediaStop, MediaOnce, MediaLoop, MediaMute, MediaUnMute, MediaSetVolume {Volume|Value}, MediaSeek {Time|Position|Value}, and for effects MediaDebugLoseContext, MediaCustomX/Y/Z.

### Media editor (MediaEdit.html)

`MediaEdit.html` boots `Sw.MediaEdit.Main` (mediaEdit.js, styles mediaEdit.css): one page to make a media ready for a web page.
- **Open** any media: the source box (a link, a YouTube link or code, the text of a text media; Enter opens it, a pasted media link opens at once), "Open file…" (a file of this computer: a `blob:` address shown only here, it can't be shared or popped out), a file or link dropped anywhere on the page (an accent frame and "Drop to open"), a link pasted with nothing focused, "Effects" (a searchable list of the effect library: an effect opens it; for an image / text it sets the effect drawn over it). The type select (Image, Video, Audio, YouTube, Effect, Text) opens the same address as another type; an address without a known extension opens as the chosen type. Without a link the page starts empty: a drop zone, "Open file…", "Paste a link" and examples.
- **Player:** the MediaView player (`CreateChrome`) in the stage, without the title bar; a text gets room around it. A new media removes the previous player first (a YouTube clip stops). A YouTube video that fails says why from YouTube's error code (`YouTubeError`: the owner allows no playing on other sites, removed / private, a bad code) with "Watch on YouTube". When the browser starts a media without sound, a "Turn on the sound" button at the top left of the player (instead of the viewer's full page overlay).
- **Settings:** the object editor of the Edit service (`Sw.Edit`, the editor of the API pages, loaded from `../edit/Edit.js`: a member menu with Set default / Copy / Paste / the media's own actions, drag and drop, its undo, changed settings marked; without the Edit service a `Sw.PropertyEditor`) of the settings of the type, as a client type (`RenderEdit`: groups, units, hints, show rules) (`Sw.MediaEdit.Schema`: labels, units, groups, rows shown only when they apply; the defaults are those of the players, `Sw.MediaEdit.Defaults`), with the effect's own `// var:` variables (`FxDefs`, from the player's `ScriptTypeInfo`) in the same grid (first for an effect). Image: show for, fill the box, transparent, crop, effect (a select of the image effects, by name), its speed / still. Video / audio / YouTube: start / end (the range is the media's length once known; ⋯ "Use the play position"), media volume, streams, size, crop. Effect: speed, offset, still, size, sharpness (DpiScale as %), quality, texture (⋯ the logo / the demo collage). Text: font (a select of web safe and generic fonts), size, bold, italic, letter spacing, font file, colors, outline, shadow, collage (separator, columns; a collage is drawn by its effect at the size of the box), effect. Changes of volume, effect speed / offset, crop, fill the box and effect variables apply at once (also to a popped out player); the others rebuild the player in place 350 ms after the last change (position, volume, loop and play state kept; a failure keeps the old player and says why). The pane head: the media name, "type · n changed", Undo / Redo (Ctrl+Z, Ctrl+Y / Ctrl+Shift+Z outside text boxes), Reset all.
- **Pop out:** MediaView.html in a window of its own (`ctrl=<prefix>`, `pos`), following every change (`Reload` with the full props, `SetVolume`, `SetProps`, `Crop`, `UpdateSize`, `Compile`, `Load` for another media); the stage shows "The player is in its own window" with "Go to the window" / "Show here"; closing that window (polled, and the viewer's `Closed`) shows the player here again. Not on phones.
- **Share:** a dialog of one size: Player page (MediaView), Media only (MediaPreview), Embed code (an iframe, one attribute per line, width, the media's aspect ratio, audio a 64 px bar); Page background (no controls, no statistics, `muted`, `loop`), Shows (player controls, statistics), Playback (start muted, loop, start where it is now), "With the site address"; Copy, Open in a new tab. Only the changed settings are in the link (`Changed()`: editor only members and default effect variables left out).
- **State:** in link mode the page address follows every change (`history.replaceState`: link, type, the changed settings), so a reload or a bookmark opens the same media and settings. ⋮: Copy settings, Paste settings…, Open the player page, New media, Appearance (only on a page of its own, not inside the app).
- **Host data mode** (unchanged contract): `get` = an api of the host app (relative to the page, must be on this site) answering `{Data, Type, Link, Edit}` for the request `id` (JSON or text; legacy name `aid`). `Edit` grants editing (truthy; with `editmask=<bits>` a number sharing a bit with the mask). `update` = the save api (relative to the page, on this site; default: `get` with "Get" replaced by "Update" in its last path part; none = no Save), posted `{...id, Data}` for an object id, else `{Id: id, Data}` (legacy `aid`: `{AssignmentId, Data}`). The source and type are the host's (read only); Save changes (Ctrl+S) / Revert at the bottom of the settings; leaving with unsaved settings asks first (`Sw.Page.SetDirty`).

`Sw.MediaView.EditMain` / `EditProps` (legacy `mediaEditMain`, `mediaEditProps`) stay the previous editor (the player with its button row and the object editor of the Edit service, `../edit/Edit.js`) for host pages that call them.

### Effect folder pages

`<body data-effect-page="background|image|collage|text">`, logic in effectPage.js. Url keys are case sensitive member names; precedence: the effect's side `.json`, the image's side `.json`, the url; keys go to the media params, then the effect params, then become shader variables (`FxProps`). Background and image pages render with `MediaPlayerEffect` directly; collage and text pages render through an iframe of `../MediaPreview.html` and relay messages (`ParseTools.MessageProxy`). Defaults: background SmoothBands; image DistortedTv on `../../logo.png`; collage Stripes on `DemoCollage.svg`; text DistortedTv ("SysWeaver", bold 300px Tahoma) or, with `Texts`, Stripes (bold 80px Tahoma, separator ",", 2 columns). `counterEffects/index.html` shows the counter demo of textEffect.js (`data-text-effect-main`).
Security (`ParseTools.UrlProblem`, the same rule as MediaView's `link`): `Effect`, `Image` and `FontUrl` must be http(s) urls of this site inside the app shell (another site is refused there and allowed in a window of its own; other schemes always). They are checked before anything is fetched, also the side `.json`; a refused one shows the reason and "Open in a new tab" (the core / ui are loaded on demand). `Text` / `Texts` from the url are limited inside the app shell (40 / 200 characters, `ParseTools.TextFromUrl`) and, as the shell's content, captioned "Text from the link": the page passes `caption=true` to its MediaPreview frame, which places the caption directly with the text and limits the text to an area in the middle (see MediaView above).
Side jsons are fetched as by the original pages (next to the effect, next to the image; a missing one is a 404 that is ignored), so a side json added next to an effect or image (also a custom one in another srcWeb folder) is used. Only the site logo (the default image, generated, never has one) is not asked for its side json (R1-Media-1).

### Galleries

`<body data-effects="Background|Image|Collage">`, logic in effectsLib.js: the names come from `Api/Get*Effects` (cached 30 s); a card per effect is created at once and the page reports itself loaded; thumbnails (640×360 at t = 5 s, `MediaPlayerEffect.GetImage`, cached in localStorage `SysWeaver.EffectInfo.<kind>.<hash>` and re-validated by ETag) render 2 (phones / touch) or 3 (else) at a time (`Parallel`), cards in (or near) the view first. The page never looks empty: each card shows at once its last thumbnail from localStorage (`ShowCached`, class `fx-has-thumb`; re-validated when its turn comes) or a static placeholder (`div.fx-ph`: the effect name over a soft pattern, on the image / collage galleries over the dimmed source picture `img.fx-ph-src`). Every card shows its state (`data-state` and the info text, a bar at the bottom of the thumbnail): `queued` "Waiting (n of m)", `rendering` "Rendering..." (a small spinner), `done` "Compilation duration x ms - Draw time y ms" (the draw time is left out when unknown, never "0.000" or "-"), or `failed` "Failed to render: reason" (also after 20 s, "it took too long"; the next card starts). `GetImage` waits at most 1.5 s for a GPU draw time (0.4 s when no timer result arrives at all), so one effect can not keep the later cards waiting.
**GPU caveat:** the thumbnails are rendered with WebGL in the visitor's browser. Without a GPU (headless browsers, remote desktops, software rendering such as SwiftShader) each card takes about a second and draw times are unknown (the cards show only the compilation time); a gallery of 40 effects then needs 30 s or more to fill. That is expected; the cards show their state meanwhile, and the thumbnails are cached for the next visit. A card opens `<folder>/?Effect=<name>` (+ `&Image=../logo.png` / `&Image=DemoCollage.svg`) in the same frame (a click on the thumbnail, or the name link: click, Enter); a plain click elsewhere on the card copies that url, as the original card did (shared copy model, also Ctrl+C on the focused card, its menu on a right click / long press, and the copy button), relative to the gallery page. Failed effects show the error on their card.

### TextDemo and MapDemo

TextDemo (`Text`, `Texts`): 8 TextStyle presets; per card a copy-style-as-JSON button, 5 image effect buttons (BadVHS, Rain, SliceGlitch, OldMovie, 3D-flag) and 4 collage effect buttons (3d-tiles, Stripes, Scroll, Wheel; style at half scale), each with an icon, its effect name as visible label, a tool tip and an accessible name with the card, in two labelled groups ("Effect:", "Collage:"; neutral colors), and the rendered text (opens the None effect); a plain click elsewhere on the card copies the url of the None effect page (as the original card; also Ctrl+C on the focused card and its menu).
MapDemo (`key`, `center`): sections of MapView cards (types, pin styles, pin sizes, zoom, DPI, links, languages, misc) with open (same frame) and copy url buttons; the card itself copies its map url on a plain click (`MapView.html?key=...`, relative as the original; shared copy model, also Ctrl+C on the focused card, its menu on a right click / long press), like the original card. Every map is a WebGL context and browsers drop the oldest above about 16 (the original demo loaded all 48 maps at once, so some went blank), so `LiveMaps` loads the maps near the view (IntersectionObserver), all of them at once like the original, and keeps a map that left the view loaded until its slot is needed; at most 14 are loaded, a map in view above that shows a "Show this map" button (`div.md-paused`; it unloads the oldest). Without a Google Maps key a note says the Google types are shown with OpenStreetMap, and each Google type MapView warns about the missing key in the console (as the original).

### MapView

See the header of mapView.js for all parameters. The key comes from the url or `Api/GetGoogleMapsKey` (Dev role); without a key OpenStreetMap is used. With a screenshot host: no fades and `doIt(0, 0, null)` 500 ms after the map frame loaded (3 arguments, as before). Url `pinX` / `pinY` now override the preset anchor (as documented; the preset used to win). `green-static-flag` uses the green flag (its image did not exist). Security: `pinX` / `pinY` must be plain lengths (`MapView.CssLength`: a number with px, %, em, rem, vw, vh, vmin or vmax; else the default) before they go into the css custom properties, an own `pin` image must be a http(s) url (or `data:image/`), and `language` is url encoded in the Google links. Inside the app shell (`MapView.InApp`, the media rule): an own `pin` image of another site is not loaded (same site or `data:image/` only, a `data:` pin of at most 64 KB), every pin is shown at most 128 px high and wide (`MaxPinPx`, `pinHeight` is clamped), and an own pin picture adds "The pin is a picture from the link" to the caption; a `link` of this site must be a page (`MapView.IsPageUrl`, the rule of `Sw.Url.IsPageUrl`: no raw server method such as `Api/...`, it is ignored); a `link` of this site is shown in the shell's content frame (`NavigateInApp`: the content page's `Sw.Page.Navigate(url, "_self")`, or `IframeNavigating` when the map is the content frame itself); a link to another site (also the Google links) opens in a new tab (`noopener noreferrer`) and shows the badge "Opens <host> in a new tab ↗" (`Map-External`, bottom left; also in its accessible name); a custom `title` is limited to 80 characters (control and bidi characters removed) and stays the plain tool tip / accessible name of the link (no caption: a title is a title). Tool tips are device neutral ("View using Google Maps", 'Open "<url>"'). Outside the app shell (headless capture, another host) the old behavior stays (any http(s) pin / link, new tab).

### State and persistence

- `localStorage["SysWeaver.Media.ShowStats"]`: the stats panel of the MediaView player is open ("true").
- `localStorage["SysWeaver.Media.Mute" | "SysWeaver.Media.Loop" | "SysWeaver.Media.Volume"]`: viewer preferences (a value under the old `QuizzWeaver.Media*` name is still read until a new one is saved).
- `localStorage["SysWeaver.EffectInfo.<kind>.<hash>"]`: gallery thumbnails; `localStorage["SysWeaver.Media.EffectDpiScale"]`: adaptive effect DPI (mediaPlayer).

## JavaScript reference

### `media.js` — `Sw.MediaView`

| Member | Description |
|---|---|
| `Prefs` | `MuteKey`, `LoopKey`, `VolumeKey`, `Muted()`, `Loop()`, `Volume()`, `SetMuted(v)`, `SetLoop(v)`, `SetVolume(v)`. |
| `GetTypeFromUrl(url)` | Media type from the extension, or -1 (legacy `mediaGetTypeFromUrl`). |
| `CreatePlayer(target, type, data, props, noControls, before, noStats, fit, aspectX, aspectY, alignX, alignY)` | Player with preview box, stats and controls → `[player, controls, stats]` or undefined (legacy `mediaCreatePlayer`, same arguments). Adds `SetButtonState`, `UpdateSize`, `TopElement`, `Stats`, `Controls`, `MessageElement` to the player. |
| `PlayWithPopup(player)` | Apply the preferences and start (legacy `mediaPlayWithPopup`). |
| `RebuildPlayer(player, create)` | Rebuild in place keeping the playback state → the new player. |
| `EditProps(target, playerRes, type, data, props, canEdit, saveFn, messagePrefix, extraButtons)` | Property editor (legacy `mediaEditProps`; loads the Edit service object editor on demand; returns true when an editor was added). |
| `EditMain()`, `ViewMain()`, `PreviewMain()` | Page boots (legacy `mediaEditMain`, `mediaViewMain`, `mediaPreviewMain`). |
| `SsHack` | Logging screenshot host (legacy global `SsHack`). |
| `Ready` | Promise: the player library is loaded. |

Internal: `ReadMediaParams`, `ParseYouTube`, `ParseYouTubeTime`, `ParseMediaLink`, `ShowLink`, `CreateStats`, `CreateControls`, `CreateChrome` (the MediaView player controls), `FormatTime`, `OpenEndedLength`, `AskEditorProps`, `MessagePrefix`, `GetScreenShotHost`, `WaitResize`, `FailReason`, `IsShellContent`, `AddTextCaption`, `PlaceTextCaption`, `TrackTextCaption`.

### `effectPage.js` — `EffectPage` (also `Sw.Media.EffectPage`)

`EffectDefaults(transparent)`, `TextDefaults(collage)`, `SideJson(url, prefix)`, `Apply(p, props, ep, fxParams, jsonUrls)`, `Link(props, effect, ep, fxParams)`, `SignalLoaded()`, `Play(player)`, `Frame(p, link, type, props)`, `Main()`.

### `effectsLib.js` — `Sw.Media.EffectsLib`

`Kinds` (gallery definitions), `Main()`; internal `CreateCard`, `RenderAll`.

### `textDemo.js` — `Sw.Media.TextDemo`; `mapDemo.js` — `Sw.Media.MapDemo`

`Main()`, `ImageEffects`, `CollageEffects`; `Main()`.

### `mapView.js` — `MapView` (also `Sw.Media.MapView`)

`OpenStreetMap`, `Pins`, `Delay(ms)`, `Coordinate(text, negLetter)`, `GetKey(p)`, `GetScreenShotHost()`, `Main()` (the map frame is created at once, as the original page: no probe, no offline placeholder).

### `parseTools.js` — `ParseTools` (also `Sw.Media.ParseTools`)

`DecodeObject(val)` (`$rgb[a]` / `$rrggbb[aa]` colors, JSON), `MessageProxy(iframe)`, `SetToObject(obj, key, val, onNotFound, logPrefix)`, `ParamsToObject(searchParams, dest, onNotFound, logPrefix)`, `ObjectToObject(src, dest, onNotFound, logPrefix)`, `ObjectToParams(src)`, `FileToObject(url, dest, onNotFound, logPrefix)`.

### `textEffect.js` — `TextEffect`, `CounterEffectParams` (also `Sw.Media.*`), `textEffectMain`

`TextEffect.LoadPlayers()` (loads media_player.js if needed, waits for `MediaPlayer.Ready`, loads the image / effect player scripts), `CreateTextEffect(effectName, text, textStyle, effectParams)`, `CreateCounterEffect(effectName, textStyle, counterParams, effectParams)` (player with `GetValue(time)`, `OnNewSize(nw, ow)`, `OnRender`). `CounterEffectParams`: Fade, ExtraSpacing, MaxDigits, FlipSpeed, FlipFraction, DecimalDigits, ThousandsSeparator, DecimalSeparator, Suffix, Prefix, DigitSpacing.

## CSS reference

### `media.css`

| Selector | Use |
|---|---|
| `body.media-page` | MediaView body. |
| `Media-View` | Viewer grid (preview, stats, controls); smaller inset with `.SwMobile`. Used with `no_controls`. |
| `Media-View.MediaChrome` (`.MediaChromeOn`, `.MediaPaused`, `.MediaAudioOnly`; local colours `--media-*`) | The player stage of MediaView (black, full window); the classes show the bars / the big Play button. |
| `.media-chrome`, `.media-top`, `.media-title`, `.media-bar`, `.media-bar-row`, `.media-chrome-part`, `.media-chrome-button`, `.media-stop`, `.media-timeline`, `.media-volume`, `.media-time`, `.media-bigplay`, `.media-audio-art`, `.media-stats-panel` | The player controls (CreateChrome): white on a dark shade over any picture, whatever the theme. |
| `Media-Preview` (`--maxWidth`), `.Replacing`, `.NoVisual` | Player box; off-screen rebuild; audio only. |
| `Media-AspectContainer` (`.MediaPreviewFit` / `.MediaPreviewFill`, `[role=button]`, `:focus-visible`), `> *.MediaHidden` | Clip box. |
| `Media-Overlay` (`.Show`) | Crop visualization. |
| `Media-Stats`, `Media-Stat` (`.Adjust`, `.Clip`, `.Actual`, `.Hide`, `--stat`), `Media-StatName`, `Media-StatValue` | Stats chips (touch height on `.SwMobile`). |
| `Media-Controls`, `.media-button` | Button row (Sw buttons). |
| `Media-NoAudio` | Click for audio overlay. |
| `Media-Full` (`.Hide`), `Media-Edit` | Editor. |
| `body.MediaPreview` (`.NoFade`: `--c0`, `--c1`, `--hs`, `--s`) | Preview / screenshot page. |
| `.IconMediaPreviewPlay/Pause/Stop/Mute/UnMute/VolLoud/VolLow/Expand/Loop/Once` (old `.MediaIcon*` aliases) | Button icons (`../icons/preview_*.svg`). |

### `effectsLib.css`, `demo.css`, `textEffect.css`

`.fx-page`, `.fx-list`, `.fx-card` (`.fx-failed`), `.fx-title`, `.fx-name` (an `a href` link to the effect page), `.fx-info`, `.fx-frame`, `.fx-thumb`, `canvas.MediaHidden` (off-screen render canvas, must keep opacity > 0); `.td-*` (TextDemo) and `.md-*` (MapDemo) cards; textEffect.css: the counter demo body.
