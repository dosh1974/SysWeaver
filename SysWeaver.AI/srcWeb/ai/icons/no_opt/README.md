# no_opt

[⬆ icons](../README.md)

> Colour smiley images returned by the `GetPredefinedImage` default AI tool, embedded without SVG optimisation.

| | |
|---|---|
| **Project** | [SysWeaver.AI](../../../../README.md) |
| **Kind** | Icons |

## Purpose

The `GetPredefinedImage` tool in [`AiServiceBase_DefaultTools.cs`](../../../../AiServiceBase_DefaultTools.cs) lets the model show a predefined image (`AiImages`); the smiley values map to the files in this folder, returned as `../ai/icons/<name>.svg`.

Unlike the parent folder, these files skip SvgOpt: the build step compresses `srcWeb\ai\icons\no_opt\*` with Brotli directly into `web/ai/icons/`, so they end up in the same url folder as the optimised icons.

## Contents

| Item | Description |
|---|---|
| [`Smiley_Angry.svg`](Smiley_Angry.svg) | `AiImages.AngrySmiley` |
| [`Smiley_HappyCrying.svg`](Smiley_HappyCrying.svg) | `AiImages.HappySmiley` |
| [`Smiley_Love.svg`](Smiley_Love.svg) | `AiImages.LoveSmiley` |
| [`Smiley_Sad.svg`](Smiley_Sad.svg) | `AiImages.SadSmiley` |
| [`Smiley_Tounge.svg`](Smiley_Tounge.svg) | `AiImages.TeasingSmiley` |

## Relationships

- **Part of:** [SysWeaver.AI](../../../../README.md)
- **Used by:** [`AiServiceBase_DefaultTools.cs`](../../../../AiServiceBase_DefaultTools.cs) (`GetPredefinedImage`)
