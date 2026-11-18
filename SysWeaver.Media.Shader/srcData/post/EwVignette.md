# EwVignette

Post: rectangular vignette multiplier from 0..1 screen uv (spread 16 = 1.0 at the center).

## Signature

```glsl
float EwVignette(vec2 uv, float spread, float power, float strength)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| uv | vec2 | Screen uv in [0, 1], `fragCoord / iResolution.xy` (not aspect corrected, not centered). |
| spread | float | Scale of the base term. 16.0 makes the center exactly 1.0; larger values push the darkening further out (center > 1). |
| power | float | Falloff exponent; 0.1 .. 0.5 is typical, smaller values give a flatter center and a sharper edge. |
| strength | float | Blend between no vignette (0) and the full curve (1). |

## Returns

`float` multiplier: 0 at the screen edges (with strength 1), 1 at the center (with spread 16).
Can exceed 1 when spread > 16.

## Description

`base = uv.x * uv.y * (1 - uv.x) * (1 - uv.y) * spread`, then `mix(1.0, pow(base, power), strength)`.
The base term peaks at 1/16 at the center, so spread 16 normalizes it. The shape follows the screen
rectangle (it is not a circle), which suits full screen effects. Multiply the final color by it.
Cost: a few multiplies and one `pow`.

## Requires

None.

## Example

```glsl
vec2 uv = fragCoord / iResolution.xy;
col *= EwVignette(uv, 16.0, 0.25, 0.8);
```

## Replacing local implementations

- `pow(16.0 * uv.x * uv.y * (1.0 - uv.x) * (1.0 - uv.y), 0.25)` == `EwVignette(uv, 16.0, 0.25, 1.0)`.
- `(1.0 - S) + S * pow(16.0 * uv.x * uv.y * (1.0 - uv.x) * (1.0 - uv.y), P)` == `EwVignette(uv, 16.0, P, S)`.
- The "Unused" template's `vignetteMultiplier(screenUV)` with `VignetteSpread`, `VignettePower`,
  `VignetteIntensity` == `EwVignette(screenUV, VignetteSpread, VignettePower, VignetteIntensity)`.

Radial vignettes (`1.0 - k * dot(p, p)` with centered `p`, `smoothstep` on `length(p)`) are a
different shape; keep them local.
