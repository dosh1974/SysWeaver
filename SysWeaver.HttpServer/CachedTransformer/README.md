# CachedTransformer

[⬆ SysWeaver.HttpServer](../README.md)

> Base class and contracts for HTTP transformers that convert served files into alternative variants and cache them on disc.

| | |
|---|---|
| **Project** | [SysWeaver.HttpServer](../README.md) |
| **Kind** | Source code |

## Purpose

A transformer service registers conversions by file extension or MIME type. [`CachedTransformer`](CachedTransformer.cs) implements `IHttpTransformerService` and handles the shared work: computing a cache key from the local URL and the source ETag, checking whether the variants already exist on disc, building missing variants (directly or queued to background build tasks, per `CachedTransformerBuildStrategies`), choosing the variant the client accepts (`Accept-Encoding` for compressed files, `Accept` for WebP / AVIF), and pruning entries that haven't been accessed for a number of days. Concrete conversions implement `ICachedTransformer`.

## Contents

| Item | Description |
|---|---|
| [`CachedTransformer.cs`](CachedTransformer.cs) | The base transformer service: registration, cache lookup, variant selection, build queue, pruning, stats and debug table |
| [`CachedTransformer.BuildJob.cs`](CachedTransformer.BuildJob.cs) | `CachedTransformerFile` (the source file to validate or build for: key, MIME type, base file name on disc, decoder) and the internal build job |
| [`CachedTransformerEntry.cs`](CachedTransformerEntry.cs) | The (possibly still building) set of cached variants of a source file, in order of preference |
| [`CachedTransformerBuildStrategies.cs`](CachedTransformerBuildStrategies.cs) How missing variants are built: `AlwaysDefer` (in the background, the original is served meanwhile), `AlwaysDirect` (the request waits) or `CheckAccept` (defer only if the client accepts the original MIME type) |
| [`CachedTransformerParams.cs`](CachedTransformerParams.cs) | Base parameters: concurrent build limits, cache folders (files distributed by hash) and the days to keep unused entries |
| [`ICachedTransformer.cs`](ICachedTransformer.cs) | The contract of one conversion: info text, build strategy, validate existing variants, build variants |

## Relationships

- **Part of:** [SysWeaver.HttpServer](../README.md)
- **Used by:** [SysWeaver.HttpTransformer.Compress](../../SysWeaver.HttpTransformer.Compress/README.md), [SysWeaver.HttpTransformer.Image](../../SysWeaver.HttpTransformer.Image/README.md), [SysWeaver.HttpTransformer.Svg](../../SysWeaver.HttpTransformer.Svg/README.md), [SysWeaver.HttpTransformer.Translate](../../SysWeaver.HttpTransformer.Translate/README.md)
