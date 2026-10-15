# LayerInfo

[⬆ Layers](../README.md)

> Typed "additional layer information" records: Unicode layer names, layer groups (sections), layers stored in the global info block, and a raw fallback.

| | |
|---|---|
| **Project** | [SysWeaver.Media.Psd](../../../README.md) |
| **Kind** | Vendored library (source code) |

## Purpose

Additional layer information records are keyed by a four-character code. [`LayerInfoFactory`](../LayerInfo.cs) creates the classes in this folder for the keys it understands and keeps every other record as `RawLayerInfo`, so unknown data is preserved when the document is saved.

Part of the vendored Paint.NET PSD plugin code (see [`PsdFile/`](../../README.md)).

## Contents

| Item | Description |
|---|---|
| [`LayerUnicodeName.cs`](LayerUnicodeName.cs) | `luni`: the layer name as a Unicode string |
| [`LayerSectionInfo.cs`](LayerSectionInfo.cs) | `lsct` / `lsdk`: layer group (section) start and end markers, with the `LayerSectionType` and `LayerSectionSubtype` enums |
| [`InfoLayers.cs`](InfoLayers.cs) | `Layr` / `Lr16` / `Lr32`: layers stored in the global additional info block (Photoshop does this for 16 and 32 bit documents) |
| [`RawLayerInfo.cs`](RawLayerInfo.cs) | Any other record, kept as raw bytes |

## Relationships

- **Part of:** [Layers](../README.md), [SysWeaver.Media.Psd](../../../README.md)
- **Uses:** [`LayerInfo.cs`](../LayerInfo.cs) (base class and factory)
