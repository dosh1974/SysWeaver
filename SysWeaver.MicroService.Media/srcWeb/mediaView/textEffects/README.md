# textEffects

[⬆ mediaView](../README.md)

> Entry page that renders stylised text, either one text or a collage of texts, optionally with an effect.

| | |
|---|---|
| **Project** | [SysWeaver.MicroService.Media](../../../README.md) |
| **Kind** | Embedded web assets (page) |

## Purpose

This folder holds no shaders of its own. [`index.html`](index.html) reads the text style (font, fill/stroke, gradients, shadow, margins) and the effect from the URL. With `Text`, it renders a single text using an effect from [`../imageEffects/`](../imageEffects/README.md) (default `DistortedTv`). With `Texts`, it renders a separated list of texts as a collage using an effect from [`../collageEffects/`](../collageEffects/README.md) (default `Stripes`). The result is shown in an iframe of `../MediaPreview.html` as media type 5 (Text). [`../TextDemo.html`](../TextDemo.html) builds its preview URLs against this page (`textEffects/?...`).

## Contents

| Item | Description |
|---|---|
| [`index.html`](index.html) | Builds text/effect parameters from the URL and shows the rendered text |

## Relationships

- **Part of:** [mediaView](../README.md) of [SysWeaver.MicroService.Media](../../../README.md)
- **Uses:** [`../imageEffects/`](../imageEffects/README.md), [`../collageEffects/`](../collageEffects/README.md), [`../parseTools.js`](../parseTools.js), [`../MediaPreview.html`](../MediaPreview.html)
- **Used by:** [`../TextDemo.html`](../TextDemo.html)
