# EwDither

Post: animated screen space dither offset (+-0.5 / 255 * strength) against banding.

## Signature

```glsl
float EwDither(vec2 fragCoord, float time, float strength)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| fragCoord | vec2 | Pixel coordinate. |
| time | float | `iTime` for animated (temporal) dither, `0.0` for a static pattern. |
| strength | float | 1.0 = +-0.5 of one 8-bit step; 2.0 hides banding in very dark gradients better. |

## Returns

`float` offset in [-0.5, 0.5) * strength / 255. Add it to the final (gamma encoded) color.

## Description

White noise from [EwHash2To1](../hash/EwHash2To1.md) centered on zero and scaled to one 8-bit
quantization step. Adding it right before output turns visible banding in smooth gradients into
invisible noise. The time offset uses `fract(time)` so the hash input stays small and precise.
Apply it after tone mapping and sRGB encoding (it is meant for the 8-bit output).

## Requires

- [EwHash2To1](../hash/EwHash2To1.md)

## Example

```glsl
col = EwLinearToSrgbFast(col);
col += EwDither(fragCoord, iTime, 1.0);
fragColor = vec4(col, 1.0);
```

## Replacing local implementations

The effects' `(hash12(fragCoord + fract(iTime) * 91.7) - 0.5) * k` maps to
`EwDither(fragCoord, iTime, k * 255.0)` (e.g. `k = 0.008` -> strength 2.04, `k = 1.0 / 255.0` ->
1.0). Different time multipliers (113.0) or hash functions only change which noise pattern you get,
not its look. Dither with a per channel vec3 hash is slightly less visible but needs three hashes;
`vec3(EwDither(...))` is usually enough.
