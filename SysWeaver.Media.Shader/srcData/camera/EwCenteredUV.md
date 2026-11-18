# EwCenteredUV

Camera: aspect correct screen coordinates with the origin at the center and y in [-0.5, 0.5].

## Signature

```glsl
vec2 EwCenteredUV(vec2 fragCoord, vec2 resolution)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| fragCoord | vec2 | Pixel coordinate (`fragCoord` in ShaderToy). |
| resolution | vec2 | Viewport size in pixels (`iResolution.xy`). |

## Returns

`vec2` = `(fragCoord - 0.5 * resolution) / resolution.y`: y in [-0.5, 0.5], x in [-0.5 * aspect, 0.5 * aspect].

## Description

Divides by the height only, so circles stay round on any aspect ratio. One unit = screen height.
With a pinhole camera, `normalize(vec3(uv, focalLength))` gives a vertical field of view of
`2 * atan(0.5 / focalLength)`.

## Requires

None.

## Example

```glsl
vec2 uv = EwCenteredUV(fragCoord, iResolution.xy);
float ring = smoothstep(0.01, 0.0, abs(length(uv) - 0.3));
```

## Replacing local implementations

- `(fragCoord - 0.5 * iResolution.xy) / iResolution.y` / `(fragCoord - iResolution.xy * 0.5) / iResolution.y`: identical.
- `(2.0 * fragCoord - iResolution.xy) / iResolution.y` (y in [-1, 1]): equals `2.0 * EwCenteredUV(fragCoord, iResolution.xy)`.
  Keep the factor 2, otherwise the zoom / field of view changes.
- `(fragCoord / iResolution.xy - 0.5) * vec2(iResolution.x / iResolution.y, 1.0)`: identical.
- Versions dividing by `iResolution.x` or `min(iResolution.x, iResolution.y)` scale differently: do not replace
  blindly (multiply by `iResolution.y / iResolution.x` etc.).
