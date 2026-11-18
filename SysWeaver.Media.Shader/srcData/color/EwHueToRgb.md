# EwHueToRgb

Color: hue -> fully saturated RGB. Piecewise linear rainbow ramp.

## Signature

```glsl
vec3 EwHueToRgb(float hue)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| hue | float | Hue, 0..1 is one full turn; wraps (0 = red, 1/3 = green, 2/3 = blue). |

## Returns

`vec3` rgb in [0, 1] with saturation and value 1.

## Description

Equivalent to `EwHsvToRgb(vec3(hue, 1.0, 1.0))` but cheaper. The ramp has visible kinks at the
primary / secondary colors (bright bands at yellow, cyan, magenta); use
[EwHueToRgbSmooth](EwHueToRgbSmooth.md) for a softer rainbow. For partially saturated colors:
`value * mix(vec3(1.0), EwHueToRgb(hue), saturation)`.

## Requires

None.

## Example

```glsl
vec3 col = EwHueToRgb(angle / 6.28318530718 + iTime * 0.1) * glow;
```

## Replacing local implementations

`hue(float h)` / `hue2rgb(float h)` with `abs(mod(h * 6.0 + vec3(0.0, 4.0, 2.0), 6.0) - 3.0) - 1.0`
are identical. The variant `abs(fract(h + vec3(1.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0`
is the same ramp. A smoothstepped version (`rgb * rgb * (3.0 - 2.0 * rgb)`) maps to
EwHueToRgbSmooth. Cosine rainbows (`0.5 + 0.5 * cos(...)`) are a different shape, see
[EwCosinePalette](EwCosinePalette.md).
