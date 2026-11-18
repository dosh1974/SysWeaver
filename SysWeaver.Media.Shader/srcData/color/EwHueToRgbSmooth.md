# EwHueToRgbSmooth

Color: hue -> fully saturated RGB, smoothed. iq's smooth HSV ramp without the bright kinks of the
linear version.

## Signature

```glsl
vec3 EwHueToRgbSmooth(float hue)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| hue | float | Hue, 0..1 is one full turn; wraps. |

## Returns

`vec3` rgb in [0, 1] with saturation and value 1.

## Description

Same ramp as [EwHueToRgb](EwHueToRgb.md), then each channel goes through `x * x * (3 - 2x)`.
The result is C1 continuous, so gradients and animated hue cycles look smoother (no bands at
yellow / cyan / magenta). Secondary colors are slightly darker than with the linear ramp.
For partially saturated colors: `value * mix(vec3(1.0), EwHueToRgbSmooth(hue), saturation)`.

## Requires

None.

## Example

```glsl
vec3 col = EwHueToRgbSmooth(fract(dist * 0.2 - iTime * 0.05));
```

## Replacing local implementations

iq's `hsv2rgb_smooth(vec3 c)` is `c.z * mix(vec3(1.0), EwHueToRgbSmooth(c.x), c.y)`. Local versions
that use `fract` instead of `mod` or a different but equivalent offset vector give the same result.
