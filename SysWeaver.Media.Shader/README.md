# SysWeaver.Media.Shader

[⬆ SysWeaver overview](../README.md)

> GLSL effects in the Effect Weaver format: parsing, the Ew snippet library, the optional vertex stage and the final WebGL program source. One implementation, so an effect loads the same way in every tool.

| | |
|---|---|
| **Layer** | Media |
| **Kind** | Library (no dependencies, namespace `EffectWeaver.Shaders`) |

## Purpose

Effect Weaver effects (ShaderToy style `mainImage`, GlslSandbox style `main`) may:

- call the **Ew snippet library** without defining the functions (`EwCenteredUV`, `EwHash2To1`, `EwDither`, `EW_PI` ...),
- read **live controls** from `iMouse` (x, y, z, w in [0, 1], described by `// iMouse.x = Name | Description | Default` comments),
- have a **vertex stage** (`void vertexMain(vec2 fragCoord)` + `varying` declarations).

This library turns such a source into a complete WebGL program, the same code for every user:

| User | How |
|---|---|
| SysWeaver media player | `MediaService.BuildEffect` (SysWeaver.MicroService.Media), called by `mediaPlayer/media_player_effect.js` for effects with one of the features above |
| Effect Saver, VjWeaver, the Effect Weaver editor | `ShaderParser.BuildSource` / `BuildParts` (D:\EffectWeaver, through EffectWeaver.Common) |

## Contents

| File | Purpose |
|---|---|
| `ShaderParser.cs` | `Parse` (variables, `// var:` annotations, iMouse channels, features) and `BuildSource` / `BuildParts` (snippets inserted, ShaderToy uniforms + `main()`, GLSL ES 3.00 or 1.00, the vertex shader after `ShaderStages.Marker`) |
| `SnippetExpander.cs` | Inserts the snippets a source uses (and their dependencies) before the first declaration using them; `#line` keeps the line numbers |
| `SnippetLibrary.cs` | The snippet library: `Builtin` (embedded, see below), `WithFolder(folder)` (the built in one with a folder's snippets on top: the Effect Weaver editor, where admins save), `Load(folder)` (a folder only) |
| `ShaderStages.cs` | The vertex stage: splits a source into the two stages, rewrites the varyings, `main()` of the vertex shader, `Marker` / `SplitProgram` |
| `srcData/` | **The source of the snippet library**: `<folder>/<Id>.glsl` + `<Id>.md` and `README.md` (the library docs, the summary table with the number of places). Every file is compressed by the build to `data/<path>.br` (brotli, `..\_tools\compress\Compress.exe -OptimizeShaders False`: stored as is, never stripped or minified) and embedded as `snippets/<path>.br` |

## The snippets

`srcData/` is the library (its [README](srcData/README.md) documents the snippets and the conventions). It used to be Effect Weaver's `_snippets`; the Effect Weaver editor's snippet folder is this folder in development (an admin's save is the source, embedded by the next build), its scanner (`_snippets/tools/SnippetScan` there) writes the summary table and the "Found in effects" sections here.

The embedded library (`SnippetLibrary`):

| Member | |
|---|---|
| `Builtin` | All embedded snippets with their code, description (`Md`, `Summary`, `Signature`), dependencies and number of places; made when first used, then shared (`Snippet.IsBuiltin`) |
| `BuiltinFiles` | The embedded files as library paths: `<folder>/<Id>.glsl`, `<folder>/<Id>.md`, `README.md` |
| `ReadBuiltinFile(path)` | The text of an embedded file (decoded once, cached), null if there is none |
| `WithFolder(folder, log)` | `Builtin` with the snippets of a folder on top: a folder snippet replaces the built in one with the same id (`Snippet.OverridesBuiltin`), new ones are added; no folder: `Builtin` |

A removed snippet must also go from `data/` (generated, not in git: clean it). The unit tests of SysWeaver.Tests (`EffectSnippetTests`) check that every effect of the media viewer and every snippet only call snippets that exist.

## Notes

- The code was `EffectWeaver.Common/Shaders` in Effect Weaver; it keeps its namespace (`EffectWeaver.Shaders`) and style (nullable, implicit usings), so Effect Weaver uses it unchanged.
- Rules the parser applies are mirrored by the Compress tool's shader minimizer (`SysWeaver.Tools/Compress/CompressProgram_Shaders.cs`): keep them in sync.
