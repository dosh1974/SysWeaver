# EwSdBox2D

SDF 2D: axis aligned box. Exact signed distance to a box centered at the origin.

## Signature

```glsl
float EwSdBox2D(vec2 p, vec2 halfSize)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec2 | Sample position relative to the box center (translate / rotate `p` before calling to place the box). |
| halfSize | vec2 | Half extents of the box; the box spans `-halfSize .. halfSize`. |

## Returns

`float` signed distance: negative inside, 0 on the border, positive outside (in the units of `p`).

## Description

Folds `p` into the first quadrant with `abs()`, then `d = abs(p) - halfSize`. Outside the box the
distance is `length(max(d, 0.0))` (exact, including the round distance field around the corners),
inside it is `max(d.x, d.y)` (distance to the nearest edge, negative). The result is exact
everywhere, so it is safe for raymarching, offsetting (`d - r` gives a box grown by `r` with round
outer corners) and anti-aliasing. Very cheap.

For rounded corners that stay inside `halfSize` use [EwSdRoundedBox2D](EwSdRoundedBox2D.md).

## Requires

None.

## Example

```glsl
float d = EwSdBox2D(uv - vec2(0.2, 0.0), vec2(0.3, 0.1));
float mask = EwFillAA(d, fwidth(d));
```

## Replacing local implementations

Local `sdBox(vec2 p, vec2 b)` functions using `d = abs(p) - b` followed by
`length(max(d, 0.0)) + min(max(d.x, d.y), 0.0)` are identical: `sdBox(p, b)` -> `EwSdBox2D(p, b)`.
Make sure the local `b` is a half size (most are); if a local version takes the full size, pass
`size * 0.5`. Versions returning only `max(d.x, d.y)` are a cheaper bound (inexact outside near the
corners); the replacement keeps the shape and only changes the field outside the corners.
