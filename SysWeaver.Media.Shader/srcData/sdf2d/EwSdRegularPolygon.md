# EwSdRegularPolygon

SDF 2D: regular N-gon. Signed distance to a regular polygon with `sides` sides, using a polar fold
into one sector.

## Signature

```glsl
float EwSdRegularPolygon(vec2 p, float radius, float sides)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec2 | Sample position relative to the polygon center. |
| radius | float | Circumradius (center to vertex). The apothem (center to edge midpoint) is `radius * cos(3.14159265 / sides)`. |
| sides | float | Number of sides, >= 3.0. A float so it can be animated; fractional values give an irregular, seamed shape. |

## Returns

`float` signed distance: negative inside, 0 on the border, positive outside.

## Description

The angle `atan(p.x, p.y)` (measured from +y, clockwise) is folded into one sector of width
`2 * pi / sides` and mirrored, so every point is mapped to the half sector between an edge
midpoint and a vertex. The vertex sits at `radius * (cos(pi / n), sin(pi / n))` in the folded
frame, the edge is the vertical line through it; the clamped distance to that edge piece, signed by
the side of the edge line, is returned. This is exact inside and outside (round fields around the
vertices), so it is safe for raymarching extrusions, outlines and offsets.

Orientation: one vertex points straight up (+y). For odd `sides` the bottom is a flat edge, for
even `sides` the bottom is a vertex (`sides = 4.0` is a diamond; rotate `p` by 45 degrees for an
axis aligned square, or use [EwSdBox2D](EwSdBox2D.md)).

Cost: one atan, one mod, sin / cos (two of them only depend on `sides`, the compiler hoists them
when `sides` is constant). For a fixed triangle the exact
[EwSdEquilateralTriangle](EwSdEquilateralTriangle.md) is cheaper; for hexagonal metrics
[EwSdHexagonFast](EwSdHexagonFast.md).

## Requires

None.

## Example

```glsl
float d = EwSdRegularPolygon(uv, 0.35, 5.0);
float mask = EwFillAA(d, fwidth(d));
```

## Replacing local implementations

The common effect version `sdRegularPolygon(point, radius, sideCount)` with
`mod(atan(point.x, point.y), 2.0 * an) - an` and `length(point) * sign(point.x)` is identical; pass
an int side count as `float(sideCount)`. Variants that differ:

- Apothem based versions (`radius` = distance to the edge, e.g. `cos(an) * length(p) - r` style
  bounds): pass `radius / cos(3.14159265 / sides)` to keep the size.
- Versions using `atan(p.y, p.x)` (angle from +x) are rotated by 90 degrees; swap to `p.yx` or rotate
  `p` to keep the orientation.
- Cheap bound versions (`cos(floor(.5 + a / an) * an - a) * length(p) - r`) are not exact outside
  near the vertices; the replacement keeps the shape but rounds the outside field.
