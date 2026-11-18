# EwLinearToSrgbFast

Color: linear -> sRGB, gamma 2.2 approximation `pow(c, 1 / 2.2)`.

## Signature

```glsl
vec3 EwLinearToSrgbFast(vec3 c)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| c | vec3 | Linear rgb. Negative values are clamped to 0 (pow of a negative is undefined in GLSL). |

## Returns

`vec3` gamma encoded color.

## Description

One `pow` per channel. Close to the real sRGB curve over most of the range but darker in the deep
shadows (no linear toe). This is the common ShaderToy "gamma correction" step. Use
[EwLinearToSrgb](EwLinearToSrgb.md) when exact sRGB matters (e.g. matching UI colors).

## Requires

None.

## Example

```glsl
col = EwTonemapReinhard(col);
fragColor = vec4(EwLinearToSrgbFast(col), 1.0);
```

## Replacing local implementations

The effects use `pow(col, vec3(0.4545))` in about 50 places; `0.4545` equals `1.0 / 2.2` to four
digits, so the replacement is visually identical. The `max(c, 0.0)` clamp only changes results for
negative inputs, which were undefined (NaN on some GPUs) in the local versions. `sqrt(col)` (gamma 2.0)
and `pow(col, vec3(1.0 / 2.4))` are different curves; keep them or accept a slight brightness change.
