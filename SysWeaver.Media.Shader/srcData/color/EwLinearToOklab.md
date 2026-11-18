# EwLinearToOklab

Color: linear sRGB -> Oklab, a perceptual color space for even gradients and color mixes.

## Signature

```glsl
vec3 EwLinearToOklab(vec3 c)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| c | vec3 | Linear rgb ([EwSrgbToLinear](EwSrgbToLinear.md) for an sRGB color). |

## Returns

`vec3` Oklab: x = L (lightness, 0 black .. 1 white), y = a (green .. red), z = b (blue .. yellow);
a and b are about -0.4 .. 0.4 for sRGB colors.

## Description

Björn Ottosson's Oklab (2020): linear sRGB to cone responses (LMS), cube root, then to L, a, b.
Distances and mixes in Oklab follow how colors look: a gradient between two colors keeps an even
brightness and hue (a mix in rgb gets muddy or too dark in the middle). Mix in Oklab, convert back
with [EwOklabToLinear](EwOklabToLinear.md). The cube root keeps the sign, so out of gamut (negative)
inputs convert back unchanged.

## Requires

None.

## Example

```glsl
// An even gradient between two sRGB colors
vec3 lo = EwLinearToOklab(EwSrgbToLinear(LowColor));
vec3 hi = EwLinearToOklab(EwSrgbToLinear(HighColor));
vec3 col = EwOklabToLinear(mix(lo, hi, uv.y));
fragColor = vec4(clamp(EwLinearToSrgb(col), 0.0, 1.0), 1.0);
```

## Replacing local implementations

Local `linear_srgb_to_oklab` / `toOklab` functions with the constants `0.4122214708`,
`0.2104542553`, ... (Ottosson's reference code) are identical.
