# EwLuminance

Color: relative luminance with Rec.709 / sRGB weights.

## Signature

```glsl
float EwLuminance(vec3 c)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| c | vec3 | Rgb color, ideally linear. |

## Returns

`float` luminance; 1.0 for white, 0.0 for black. Unbounded for HDR input.

## Description

`dot(c, vec3(0.2126, 0.7152, 0.0722))`. Correct relative luminance for linear sRGB primaries; use it
for desaturation, bloom thresholds, luminance based tone mapping and contrast. On gamma encoded colors
it is an approximation (there [EwLumaRec601](EwLumaRec601.md) is the more traditional choice).

## Requires

None.

## Example

```glsl
float brightness = EwLuminance(col);
vec3 bloom = col * smoothstep(0.8, 1.2, brightness);
```

## Replacing local implementations

`dot(col, vec3(0.2126, 0.7152, 0.0722))` maps 1:1. `vec3(0.299, 0.587, 0.114)` is Rec.601 luma, use
[EwLumaRec601](EwLumaRec601.md) to keep the result identical. `(r + g + b) / 3.0` or `length(col)`
are different measures.
