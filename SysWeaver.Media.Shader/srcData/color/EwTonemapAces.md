# EwTonemapAces

Color: ACES filmic tone mapping (Narkowicz fit), output in [0, 1].

## Signature

```glsl
vec3 EwTonemapAces(vec3 x)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| x | vec3 | Linear HDR color (multiply by an exposure first). |

## Returns

`vec3` tone mapped color in [0, 1], still linear (encode with [EwLinearToSrgb](EwLinearToSrgb.md)
or [EwLinearToSrgbFast](EwLinearToSrgbFast.md)).

## Description

`(x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14)`, clamped. A cheap rational fit with a
filmic toe (more contrast in the shadows) and a soft shoulder; bright values saturate towards white.
Compared with [EwTonemapReinhard](EwTonemapReinhard.md) it is darker for small values and brighter
for large ones. The Narkowicz fit expects the input pre-scaled by about 0.6 to match real ACES
exposure; most shaders feed the color directly.

## Requires

None.

## Example

```glsl
col = EwTonemapAces(col * Exposure);
col = EwLinearToSrgbFast(col);
```

## Replacing local implementations

Local `aces(x)` / `acesFilm(x)` with constants `2.51, 0.03, 2.43, 0.59, 0.14` are identical. Versions
written with `clamp(..., 0.0, 1.0)` or a `saturate` macro are the same. Stephen Hill's "ACESFitted"
(with input / output matrices) is a different, more accurate curve; keep it local.
