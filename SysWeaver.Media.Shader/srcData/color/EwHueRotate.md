# EwHueRotate

Color: hue rotation. Rotates the hue of an rgb color around the grey axis by an angle in radians.

## Signature

```glsl
vec3 EwHueRotate(vec3 color, float angle)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| color | vec3 | Rgb color (linear or gamma, any range). |
| angle | float | Rotation in radians; 2pi / 3 turns red into green, green into blue. |

## Returns

`vec3` rotated color. Can go slightly below 0 or above 1 for highly saturated inputs; clamp if needed.

## Description

Rodrigues rotation around `k = (1, 1, 1) / sqrt(3)`:
`c * cos(a) + cross(k, c) * sin(a) + k * dot(k, c) * (1 - cos(a))`. Greys stay unchanged, the sum
`r + g + b` is preserved and saturation (distance from the grey axis) is kept. Cheaper and branch free
compared with an [EwRgbToHsv](EwRgbToHsv.md) / [EwHsvToRgb](EwHsvToRgb.md) round trip, but the
perceived brightness changes a little with the angle (blue is darker than green at equal rgb sum).

## Requires

None.

## Example

```glsl
col = EwHueRotate(col, iTime * 0.3);
```

## Replacing local implementations

Local `hueShift(col, a)` functions using `k = vec3(0.57735)` and the Rodrigues formula are identical
(small differences in the precision of the constant). Versions taking the shift in 0..1 turns need
`angle = shift * 6.28318530718`. HSV based shifts (`hsv.x += shift`) produce a different (piecewise)
mapping; keep them as an EwRgbToHsv / EwHsvToRgb round trip if the exact look matters. YIQ based
rotations also differ slightly. A negative angle rotates the other direction.
