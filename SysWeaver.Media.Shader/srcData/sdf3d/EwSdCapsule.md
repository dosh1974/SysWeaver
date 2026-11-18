# EwSdCapsule

SDF 3D: capsule. Exact signed distance to the set of points within `radius` of the segment
`a`-`b` (a cylinder with hemispherical caps).

## Signature

```glsl
float EwSdCapsule(vec3 p, vec3 a, vec3 b, float radius)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec3 | Sample position. |
| a | vec3 | Segment start (center of the first cap). |
| b | vec3 | Segment end (center of the second cap). |
| radius | float | Capsule radius, >= 0. 0 returns the unsigned distance to the segment. |

## Returns

`float` signed distance: negative inside, 0 on the surface, positive outside.

## Description

Clamped projection of `p` onto the segment (as in [EwSdSegment2D](../sdf2d/EwSdSegment2D.md)),
distance to the closest point minus `radius`. Exact, cheap, ideal for limbs, cables, tubes and
glow lines in raymarchers (`exp(-k * EwSdCapsule(p, a, b, 0.0))` for a glow). `a == b` divides by
zero; use a sphere (`length(p - a) - radius`) in that case.

The total length of the capsule is `length(b - a) + 2.0 * radius`.

## Requires

None.

## Example

```glsl
float mapScene(vec3 p)
{
    return EwSdCapsule(p, vec3(-0.5, 0.0, 0.0), vec3(0.5, 0.3, 0.0), 0.1);
}
```

## Replacing local implementations

Local `sdCapsule(p, a, b, r)` with the clamped projection are identical. Axis aligned variants
(`sdVerticalCapsule(p, h, r)`: `p.y -= clamp(p.y, 0.0, h); return length(p) - r;`) map to
`EwSdCapsule(p, vec3(0.0), vec3(0.0, h, 0.0), r)` (same field, slightly more work). 3D segment
distance helpers (`sdSegment(vec3 p, vec3 a, vec3 b)`) map to `EwSdCapsule(p, a, b, 0.0)`. Watch
for `(a, b, p)` argument orders.
