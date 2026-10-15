# map

[⬆ mediaView](../README.md)

> SVG pin graphics that the map view can place on a map.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Media](../../../README.md) |
| **Kind** | Embedded web assets (SVG images) |

## Purpose

[`../MapView.html`](../MapView.html) maps the `pin` URL parameter to one of these images. Each entry also sets the anchor point and default height. For example `pin=true` or `pin=red-marker` uses `RedMarker.svg`, and `pin=star` uses `Star.svg`.

## Contents

| Item | Description |
|---|---|
| `*Marker.svg` (3 files) | Map markers: [`RedMarker.svg`](RedMarker.svg) (also the default for `pin=true`), [`GreenMarker.svg`](GreenMarker.svg), [`BlueMarker.svg`](BlueMarker.svg) |
| `*Needle.svg` (3 files) | Pin needles: [`RedNeedle.svg`](RedNeedle.svg), [`GreenNeedle.svg`](GreenNeedle.svg), [`BlueNeedle.svg`](BlueNeedle.svg) |
| `*Flag.svg` (4 files) | Flags: [`RedFlag.svg`](RedFlag.svg), [`GreenFlag.svg`](GreenFlag.svg), [`BlueFlag.svg`](BlueFlag.svg), [`GameFlag.svg`](GameFlag.svg) |
| [`GreenPennant.svg`](GreenPennant.svg) | Green pennant (`green-pennant`) |
| [`BlueMedieval.svg`](BlueMedieval.svg) | Medieval style blue banner (`blue-medieval`) |
| [`Star.svg`](Star.svg) | Star (`star`) |

## Limitations and considerations

- `MapView.html` also maps `green-static-flag` to `map/GreenStaticFlag.svg`, which is not in this folder.

## Relationships

- **Part of:** [mediaView](../README.md) of [SysWeaver.MicroService.Media](../../../README.md)
- **Used by:** [`../MapView.html`](../MapView.html)
