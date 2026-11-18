# EwSdBox3D

SDF 3D: axis aligned box. Exact signed distance to a box centered at the origin.

## Signature

```glsl
float EwSdBox3D(vec3 p, vec3 halfSize)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec3 | Sample position relative to the box center (translate / rotate `p` to place the box). |
| halfSize | vec3 | Half extents; the box spans `-halfSize .. halfSize`. |

## Returns

`float` signed distance: negative inside, 0 on the surface, positive outside.

## Description

3D version of [EwSdBox2D](../sdf2d/EwSdBox2D.md): `d = abs(p) - halfSize`, exact euclidean
distance outside (`length(max(d, 0.0))`), distance to the nearest face inside
(`max(d.x, d.y, d.z)`, negative). Exact everywhere, so it is ideal for sphere tracing. Rounded box:
`EwSdBox3D(p, halfSize - r) - r` keeps the outer extent at `halfSize`; `EwSdBox3D(p, halfSize) - r`
grows the box by `r`.

## Requires

None.

## Example

```glsl
float mapScene(vec3 p)
{
    return EwSdBox3D(p - vec3(0.0, 0.5, 0.0), vec3(0.5));
}
```

## Replacing local implementations

Local `sdBox(vec3 p, vec3 b)` using `q = abs(p) - b; length(max(q, 0.0)) + min(max(q.x, max(q.y,
q.z)), 0.0)` are identical. Check that `b` is a half size. Local `sdRoundBox(p, b, r)` returning
`... - r` with `q = abs(p) - b` map to `EwSdBox3D(p, b) - r`; with `q = abs(p) - b + r` to
`EwSdBox3D(p, b - r) - r`. Bound versions `max(q.x, max(q.y, q.z))` (no length) are inexact outside;
the replacement keeps the shape but can change marching step counts slightly.
