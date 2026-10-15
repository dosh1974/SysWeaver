# srcData

[⬆ SysWeaver.Map](../README.md)

> The source map data: one SVG drawing and one JSON region index per map, compressed and embedded into the assembly at build time.

| | |
|---|---|
| **Project** | [SysWeaver.Map](../README.md) |
| **Kind** | Embedded data |

## Purpose

The `EmbedLocal` target in [`SysWeaver.Map.csproj`](../SysWeaver.Map.csproj) runs before every build: it compresses every file in this folder (including sub folders, `.\srcData\*+`) with Brotli using Compress (`_tools/compress`) into `data/` and embeds every `data/**/*.br` file as a resource. The generated `data/` folder is not in the repository.

At runtime `MapCache` loads a map by name from the resources `SysWeaver.Map.data.<name>.json.br` and `<name>.svg.br`. The names come from `MapTools`: `world`, `africa`, `europe`, `north-america`, `<country>` and `zones_<country>`, where `<country>` is an ISO 3166 alpha-2 code.

Each SVG has one `path` per region whose `class` is the region name; the matching JSON file is an array of region records (`MapRegionInfo`: `N` = region name, `X`, `Y`, `W`, `H` = bounding box, `I` = original order, `CX`, `CY` = text centre).

## Contents

| Item | Description |
|---|---|
| `world`, `africa`, `europe`, `north-america`, `usa` (`.svg` + `.json`, 10 files) | Multi country maps; `usa.*` is present in the folder but not listed in `MapTools` |
| `<CC>.svg` / `<CC>.json` (239 pairs, 478 files) | The outline of a single country, e.g. `SE.svg`, `US.json` |
| `zones_<CC>.svg` / `zones_<CC>.json` (210 pairs, 420 files) | A country divided into its administrative zones (states, counties, ...), e.g. `zones_US.svg`, `zones_DE.json` |

## Relationships

- **Part of:** [SysWeaver.Map](../README.md)
- **Uses:** [Compress](../../_tools/compress/README.md) at build time
- **Used by:** [`MapCache.cs`](../MapCache.cs), [`MapTools.cs`](../MapTools.cs)
