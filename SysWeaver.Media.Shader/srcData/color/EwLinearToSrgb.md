# EwLinearToSrgb

Color: linear -> sRGB. Exact piecewise sRGB encoding for the final output color.

## Signature

```glsl
vec3 EwLinearToSrgb(vec3 c)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| c | vec3 | Linear rgb. Negative values are clamped to 0. |

## Returns

`vec3` sRGB encoded color. 0 -> 0, 1 -> 1; values above 1 are not clamped.

## Description

Per channel: `c <= 0.0031308 ? 12.92 * c : 1.055 * pow(c, 1.0 / 2.4) - 0.055`. Branch free (both
sides are computed and selected). Apply once, at the very end, after tone mapping
([EwTonemapAces](EwTonemapAces.md) / [EwTonemapReinhard](EwTonemapReinhard.md)). The cheaper
[EwLinearToSrgbFast](EwLinearToSrgbFast.md) (pure gamma 2.2) is visually close but darker near
black (no linear toe).

## Requires

None.

## Example

```glsl
col = EwTonemapAces(col * exposure);
fragColor = vec4(EwLinearToSrgb(col), 1.0);
```

## Replacing local implementations

Local `linearToSrgb` / `toSRGB` functions using `0.0031308`, `12.92`, `1.055`, `1.0 / 2.4` and
`0.055` are identical (apart from negative input handling: this one clamps to 0). Plain
`pow(col, vec3(0.4545))` / `pow(col, vec3(1.0 / 2.2))` should be replaced by
[EwLinearToSrgbFast](EwLinearToSrgbFast.md) to keep the look unchanged.
