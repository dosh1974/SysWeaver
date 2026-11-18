# EwSaturation

Color: saturation adjust. 0 = grey, 1 = unchanged, > 1 = boosted.

## Signature

```glsl
vec3 EwSaturation(vec3 color, float amount)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| color | vec3 | Rgb color. |
| amount | float | Saturation factor: 0 = greyscale, 1 = unchanged, > 1 = more saturated. |

## Returns

`vec3` adjusted color. With `amount > 1` channels can go below 0 or above 1; clamp if needed.

## Description

`mix(vec3(EwLuminance(color)), color, amount)`. Luminance (Rec.709) is preserved for any amount, so
the brightness does not shift while desaturating. Cheaper and smoother than scaling `hsv.y` in an
HSV round trip.

## Requires

- [EwLuminance](EwLuminance.md)

## Example

```glsl
col = EwSaturation(col, Saturation); // e.g. Saturation = 1.2
```

## Replacing local implementations

`mix(vec3(dot(col, vec3(0.2126, 0.7152, 0.0722))), col, s)` maps 1:1. Versions using Rec.601 weights
(`0.299, 0.587, 0.114`) or the channel average give a slightly different grey; the difference is
small for moderate amounts. `mix(col, grey, d)` (desaturate amount) is `EwSaturation(col, 1.0 - d)`.
