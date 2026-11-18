# EwHsvToRgb

Color: HSV -> RGB. Branchless conversion with all components in [0, 1].

## Signature

```glsl
vec3 EwHsvToRgb(vec3 hsv)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| hsv | vec3 | x = hue (0..1, wraps), y = saturation (0..1), z = value (0..1). |

## Returns

`vec3` rgb in [0, 1] for in-range saturation / value.

## Description

Standard branchless version (Sam Hocevar, also used by countless ShaderToy shaders). The hue goes
through `fract`, so values outside [0, 1] (e.g. `t * 0.1 + iTime`) wrap cleanly. The hue ramp is
piecewise linear; for a smoother (C1) rainbow use [EwHueToRgbSmooth](EwHueToRgbSmooth.md).
Inverse: [EwRgbToHsv](EwRgbToHsv.md). For a fully saturated color only, [EwHueToRgb](EwHueToRgb.md)
is cheaper.

## Requires

None.

## Example

```glsl
vec3 col = EwHsvToRgb(vec3(fract(iTime * 0.05 + uv.x), 0.8, 1.0));
```

## Replacing local implementations

`hsv2rgb(vec3 c)` with `K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0)` is identical. Variants taking
`(h, s, v)` as separate floats become `EwHsvToRgb(vec3(h, s, v))`. Versions using hue in degrees or
radians need `h / 360.0` or `h / 6.28318530718`. iq's smooth variant (`rgb * rgb * (3.0 - 2.0 * rgb)`)
is not identical: use `v * mix(vec3(1.0), EwHueToRgbSmooth(h), s)`.
