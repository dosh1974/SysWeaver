# EwSdEquilateralTriangle

SDF 2D: equilateral triangle. Exact signed distance (iq), centroid at the origin, apex up.

## Signature

```glsl
float EwSdEquilateralTriangle(vec2 p, float radius)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec2 | Sample position relative to the triangle centroid. Use `-p.y` (or rotate) for an apex down triangle. |
| radius | float | Half the side length. The base is at `y = -radius / sqrt(3)`, the apex at `y = 2 * radius / sqrt(3)`; the circumradius is `1.1547 * radius`, the inradius `0.57735 * radius`. |

## Returns

`float` signed distance: negative inside, 0 on the border, positive outside.

## Description

Mirrors `p` across the vertical axis, moves the base to `y = 0`, reflects the point across the
bisector so only the base edge has to be tested, clamps to the edge and returns the signed length.
Exact everywhere (including round distance fields around the vertices). Cheap, one branch.

Note that `radius` is not the circumradius: to fit a triangle with circumradius `R`, pass
`R * 0.8660254`. For other polygon counts use [EwSdRegularPolygon](EwSdRegularPolygon.md)
(`sides = 3.0` gives the same shape there with a circumradius parameter).

## Requires

None.

## Example

```glsl
float d = EwSdEquilateralTriangle(uv, 0.3);
float mask = EwFillAA(d, fwidth(d));
```

## Replacing local implementations

Local `sdTriangle(p, r)` / `sdEquilateralTriangle(p, r)` using `k = sqrt(3)` (1.7320508 or
`sqrt(3.0)`) and the reflection `vec2(p.x - k * p.y, -k * p.x - p.y) * 0.5` are identical. Generic
three point `sdTriangle(p, p0, p1, p2)` functions are a different snippet; do not replace them.
