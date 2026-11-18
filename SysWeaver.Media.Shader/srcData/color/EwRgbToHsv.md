# EwRgbToHsv

Color: RGB -> HSV. Branchless conversion with all components in [0, 1].

## Signature

```glsl
vec3 EwRgbToHsv(vec3 rgb)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| rgb | vec3 | Color, components in [0, 1]. |

## Returns

`vec3` with x = hue in [0, 1), y = saturation in [0, 1], z = value (max component).

## Description

Sam Hocevar's branchless conversion. The `1.0e-10` epsilon avoids division by zero for grey
(chroma 0, hue becomes 0) and black (saturation 0). Typical use: convert, shift hue or scale
saturation, convert back with [EwHsvToRgb](EwHsvToRgb.md). For a pure hue rotation without the
round trip see [EwHueRotate](EwHueRotate.md); for saturation changes that preserve luminance see
[EwSaturation](EwSaturation.md).

## Requires

None.

## Example

```glsl
vec3 hsv = EwRgbToHsv(col);
hsv.x = fract(hsv.x + iTime * 0.05);
col = EwHsvToRgb(hsv);
```

## Replacing local implementations

`rgb2hsv(vec3 c)` with `K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0)` and `e = 1.0e-10` is identical.
Versions with a different epsilon (e.g. `1e-6`) differ only for near-black / near-grey inputs.
