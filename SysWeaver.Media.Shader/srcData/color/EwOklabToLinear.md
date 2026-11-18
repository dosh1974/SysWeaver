# EwOklabToLinear

Color: Oklab -> linear sRGB, the inverse of [EwLinearToOklab](EwLinearToOklab.md).

## Signature

```glsl
vec3 EwOklabToLinear(vec3 lab)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| lab | vec3 | Oklab: x = L (lightness), y = a (green .. red), z = b (blue .. yellow). |

## Returns

`vec3` linear rgb. Colors made in Oklab (a mix, a shifted hue) can be outside the sRGB gamut:
values below 0 or above 1 are not clamped.

## Description

Björn Ottosson's Oklab (2020): L, a, b to cone responses (LMS), cubed, then to linear sRGB.
Encode the result for the output with [EwLinearToSrgb](EwLinearToSrgb.md) and clamp it when it
is shown.

## Requires

None.

## Example

```glsl
vec3 col = EwOklabToLinear(mix(lo, hi, t));
fragColor = vec4(clamp(EwLinearToSrgb(col), 0.0, 1.0), 1.0);
```

## Replacing local implementations

Local `oklab_to_linear_srgb` / `fromOklab` functions with the constants `0.3963377774`,
`4.0767416621`, ... (Ottosson's reference code) are identical.
