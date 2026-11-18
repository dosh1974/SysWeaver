# EwSrgbToLinear

Color: sRGB -> linear. Exact piecewise sRGB decoding, the inverse of [EwLinearToSrgb](EwLinearToSrgb.md).

## Signature

```glsl
vec3 EwSrgbToLinear(vec3 c)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| c | vec3 | sRGB encoded rgb. Negative values are clamped to 0. |

## Returns

`vec3` linear color. 0 -> 0, 1 -> 1; values above 1 are not clamped.

## Description

Per channel: `c <= 0.04045 ? c / 12.92 : pow((c + 0.055) / 1.055, 2.4)`. Branch free (both
sides are computed and selected). Colors chosen by eye (constants, `// var:` color variables,
pictures) are sRGB: decode them before blending, lighting or converting to Oklab
([EwLinearToOklab](EwLinearToOklab.md)), encode the result once at the end with
[EwLinearToSrgb](EwLinearToSrgb.md).

## Requires

None.

## Example

```glsl
vec3 lo = EwLinearToOklab(EwSrgbToLinear(LowColor));
vec3 hi = EwLinearToOklab(EwSrgbToLinear(HighColor));
vec3 col = EwOklabToLinear(mix(lo, hi, t));
fragColor = vec4(clamp(EwLinearToSrgb(col), 0.0, 1.0), 1.0);
```

## Replacing local implementations

Local `srgbToLinear` / `toLinear` functions using `0.04045`, `12.92`, `1.055`, `0.055` and `2.4`
are identical (apart from negative input handling: this one clamps to 0). A plain
`pow(col, vec3(2.2))` is the cheap approximation: replacing it changes the look near black.
