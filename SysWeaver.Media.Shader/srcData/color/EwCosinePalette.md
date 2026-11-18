# EwCosinePalette

Color: iq cosine palette. Maps a scalar to a smooth, periodic color ramp defined by four vec3 parameters.

## Signature

```glsl
vec3 EwCosinePalette(float t, vec3 a, vec3 b, vec3 c, vec3 d)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| t | float | Palette position, any range. |
| a | vec3 | Bias / center color. |
| b | vec3 | Amplitude (contrast) per channel. |
| c | vec3 | Frequency per channel (1.0 = one full cycle per unit t). |
| d | vec3 | Phase offset per channel (in cycles, 0..1). |

## Returns

`vec3` rgb color `a + b * cos(2pi * (c * t + d))`. Stays in [0, 1] when `a - b >= 0` and `a + b <= 1`.

## Description

Inigo Quilez' cosine palette. One `cos` per channel, very cheap. With integer `c` the palette repeats
seamlessly every unit of `t`, which makes it ideal for animating with `t + iTime * speed`.
Classic presets: `a = b = vec3(0.5)`, `c = vec3(1.0)`, `d = vec3(0.0, 0.33, 0.67)` (rainbow),
`d = vec3(0.0, 0.10, 0.20)`, `d = vec3(0.3, 0.20, 0.20)`. Output is not clamped.

## Requires

None.

## Example

```glsl
vec3 col = EwCosinePalette(dist * 0.5 + iTime * 0.1,
                           vec3(0.5), vec3(0.5), vec3(1.0), vec3(0.0, 0.33, 0.67));
```

## Replacing local implementations

Local `palette(t, a, b, c, d)` functions map 1:1. Watch the constant: `6.28318`, `6.2831` or `TAU`
are equivalent; some versions write `cos(6.28318 * (c * t + d))` with the phase in radians
(`cos(c * t + d)`), in which case divide `d` and `c` by 2pi. Palettes with hard-coded `a..d` inside
the function become a call with those constants as arguments.
