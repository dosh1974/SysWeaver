# Layers

[⬆ PsdFile](../README.md)

> The layer model of a PSD file: layers, their channels, masks, blending ranges and additional layer information records.

| | |
|---|---|
| **Project** | [SysWeaver.Media.Psd](../../README.md) |
| **Kind** | Vendored library (source code) |

## Purpose

Loads and saves the records of the "layer and mask information" section. Each `Layer` has a rectangle, blend mode, opacity, flags, a list of channels with their image data, optional masks and blending ranges, and a list of additional `LayerInfo` records (unicode name, layer group / section, ...). The typed `LayerInfo` records live in [`LayerInfo/`](LayerInfo/README.md).

Part of the vendored Paint.NET PSD plugin code (see [`PsdFile/`](../README.md)).

## Contents

| Item | Description |
|---|---|
| [`LayerInfo/`](LayerInfo/README.md) | Typed additional layer information records |
| [`Layer.cs`](Layer.cs) | `Layer`: rectangle, blend mode, opacity, visibility, name, channels, masks and additional info |
| [`Channel.cs`](Channel.cs) | `Channel` (one colour, alpha or mask channel with its image data) and `ChannelList` |
| [`Mask.cs`](Mask.cs) | `Mask` (layer or user mask with rectangle and flags) and `MaskInfo` |
| [`BlendingRanges.cs`](BlendingRanges.cs) | The layer blending ranges data |
| [`LayerInfo.cs`](LayerInfo.cs) | `LayerInfo` base class and `LayerInfoFactory`, which maps the four-character keys (`luni`, `lsct`, `lsdk`, `Layr`, `Lr16`, `Lr32`, ...) to the record classes |

## Relationships

- **Part of:** [PsdFile](../README.md), [SysWeaver.Media.Psd](../../README.md)
- **Uses:** [`Compression/`](../Compression/README.md) for channel image data
