# EwSdSegment2D

SDF 2D: line segment. Exact distance from a point to the segment `a`-`b`.

## Signature

```glsl
float EwSdSegment2D(vec2 p, vec2 a, vec2 b)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec2 | Sample position. |
| a | vec2 | Segment start point. |
| b | vec2 | Segment end point. |

## Returns

`float` unsigned distance >= 0. Subtract a half width to get a signed stroke (2D capsule):
`EwSdSegment2D(p, a, b) - width * 0.5`.

## Description

Projects `p` onto the line through `a` and `b`, clamps the projection parameter to [0, 1] and
returns the distance to that closest point. Exact, cheap (one division, one sqrt). The end caps are
round. `a == b` divides by zero (NaN); keep the points apart, or guard locally with
`max(dot(ba, ba), 1e-8)` if they can coincide.

The 3D equivalent with a radius is [EwSdCapsule](../sdf3d/EwSdCapsule.md).

## Requires

None.

## Example

```glsl
float d = EwSdSegment2D(uv, vec2(-0.3, 0.0), vec2(0.3, 0.2)) - 0.01;
float line = EwFillAA(d, fwidth(d));
```

## Replacing local implementations

Local `sdSegment(p, a, b)`, `distanceToSegment(p, a, b)` or `sdLine(p, a, b)` using the clamped
projection are identical. Watch the argument order: some local versions take `(a, b, p)`. Versions
that already subtract a radius map to `EwSdSegment2D(p, a, b) - radius`. Versions that do not clamp
`h` are infinite line distances, not segments; do not replace those.
