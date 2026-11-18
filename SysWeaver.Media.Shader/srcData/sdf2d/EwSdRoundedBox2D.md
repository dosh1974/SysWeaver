# EwSdRoundedBox2D

SDF 2D: box with rounded corners. Signed distance to an axis aligned rounded box whose outer
extent stays `halfSize` (the corners are cut inwards by `radius`).

## Signature

```glsl
float EwSdRoundedBox2D(vec2 p, vec2 halfSize, float radius)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec2 | Sample position relative to the box center. |
| halfSize | vec2 | Half extents of the box (outer extent, including the rounding). |
| radius | float | Corner radius, 0 .. min(halfSize.x, halfSize.y). 0 gives a sharp box, `min(halfSize)` a stadium (or a circle for a square). |

## Returns

`float` signed distance: negative inside, 0 on the border, positive outside.

## Description

Computes the exact box distance for the box shrunk by `radius` and subtracts `radius`, which rounds
the corners while the straight edges stay at `halfSize`. Exact outside; inside it is the shrunk box
field offset by `radius` (exact too). Cheap.

A radius larger than the smallest half extent makes the shape larger than `halfSize` on that axis;
clamp it if it is animated: `min(radius, min(halfSize.x, halfSize.y))`.

## Requires

None.

## Example

```glsl
float d = EwSdRoundedBox2D(uv, vec2(0.4, 0.25), 0.05);
float mask = EwFillAA(d, fwidth(d));
```

## Replacing local implementations

Two conventions exist in the effects:

- Inward rounding (`q = abs(p) - b + r; ... - r`): identical, `sdRoundedBox(p, b, r)` ->
  `EwSdRoundedBox2D(p, b, r)`.
- Outward rounding (`q = abs(p) - b; length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r`): the box
  grows by `r`, its outer extent is `b + r`. Replace with `EwSdRoundedBox2D(p, b + r, r)` to keep
  exactly the same shape.

iq's `sdRoundedBox` with a vec4 of per corner radii is a different function; replace it only when
all four radii are equal (then it is the inward convention).
