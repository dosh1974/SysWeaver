# EwCornerOpacity

Post: bilinear opacity from 4 corner values (bottomLeft, bottomRight, topLeft, topRight).

## Signature

```glsl
float EwCornerOpacity(vec2 uv, vec4 corners)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| uv | vec2 | Screen uv in [0, 1], `fragCoord / iResolution.xy` (y = 0 is the bottom). |
| corners | vec4 | Opacity at x = bottom left, y = bottom right, z = top left, w = top right. |

## Returns

`float` opacity in [0, 1], bilinearly interpolated and clamped.

## Description

Two horizontal `mix` (bottom row, top row) followed by a vertical `mix`. Used to fade an effect
towards chosen screen corners, e.g. when it is layered over a desktop or other content. Multiply the
final rgba (premultiplied) or the alpha by it.

## Requires

None.

## Example

```glsl
vec4 corners = vec4(OpacityBottomLeft, OpacityBottomRight, OpacityTopLeft, OpacityTopRight);
color *= EwCornerOpacity(fragCoord / iResolution.xy, corners);
```

## Replacing local implementations

The template's `cornerOpacity(screenUV)` / `cornerOpacityMultiplier(uv)` using the
`OpacityBottomLeft / BottomRight / TopLeft / TopRight` constants maps 1:1 to
`EwCornerOpacity(screenUV, vec4(OpacityBottomLeft, OpacityBottomRight, OpacityTopLeft, OpacityTopRight))`.
Mind the corner order. Versions that apply `smoothstep` to the uv before mixing (smoother blend) are
`EwCornerOpacity(smoothstep(0.0, 1.0, uv), corners)`.
