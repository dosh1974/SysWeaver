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
| `SnippetLibrary.cs` | The snippet library: `Builtin` (embedded, see below) or `Load(folder)` (the editor) |
| `ShaderStages.cs` | The vertex stage: splits a source into the two stages, rewrites the varyings, `main()` of the vertex shader, `Marker` / `SplitProgram` |
| `Snippets/` | The built in snippets (`<folder>/<Id>.glsl`, embedded as `snippets/<folder>/<Id>.glsl`) |

## The snippets

The source of the snippets is Effect Weaver's `_snippets` folder (edited and documented there, `D:\EffectWeaver\_snippets`). `Snippets/` is a copy: when the Effect Weaver repository is next to this one the build copies its `.glsl` files here first (`SyncEwSnippets` in the csproj), so the copy stays in sync and a checkout without Effect Weaver builds the same library. Snippets removed from `_snippets` must be removed here by hand.

## Notes

- The code was `EffectWeaver.Common/Shaders` in Effect Weaver; it keeps its namespace (`EffectWeaver.Shaders`) and style (nullable, implicit usings), so Effect Weaver uses it unchanged.
- Rules the parser applies are mirrored by the Compress tool's shader minimizer (`SysWeaver.Tools/Compress/CompressProgram_Shaders.cs`): keep them in sync.
