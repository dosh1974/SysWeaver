# EwRotate2D

Transform: 2D rotation matrix. `EwRotate2D(a) * v` rotates `v` counter-clockwise by `a` radians around the origin.

## Signature

```glsl
mat2 EwRotate2D(float angle)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| angle | float | Rotation angle in radians (positive = counter-clockwise for `m * v`). |

## Returns

`mat2` equal to `mat2(c, s, -s, c)` with `c = cos(angle)`, `s = sin(angle)` (column major: columns `(c, s)` and `(-s, c)`).

## Description

`EwRotate2D(a) * v == vec2(c * v.x - s * v.y, s * v.x + c * v.y)`, the standard counter-clockwise rotation
(y up). Multiplying from the other side, `v * EwRotate2D(a)`, uses the transpose and rotates clockwise.
Costs one `sin` and one `cos`; when the same angle rotates many points, build the matrix once outside the loop.
For a single point, [EwRotateVec2](EwRotateVec2.md) gives the same result without a matrix.

## Requires

None.

## Example

```glsl
vec2 uv = EwCenteredUV(fragCoord, iResolution.xy);
uv = EwRotate2D(iTime * 0.2) * uv;          // spin the pattern counter-clockwise
```

## Replacing local implementations

The effects use both element orders:

- `mat2(c, s, -s, c)` (also `mat2(cos(a), sin(a), -sin(a), cos(a))`): identical, replace with `EwRotate2D(a)`.
- `mat2(c, -s, s, c)`: the transpose, clockwise for `m * v`. Replace with `EwRotate2D(-a)`.
- Check how the matrix is applied: `v * m` (or `p *= m`, which is `p = p * m`) uses the transpose and flips
  the direction. So a local `mat2(c, -s, s, c)` used as `p *= rot(a)` is counter-clockwise and equals
  `EwRotate2D(a) * p`, i.e. `p *= rot(a)` can become `p *= EwRotate2D(-a)` or `p = EwRotate2D(a) * p`.
- If only the visual motion matters (a free running animation), the direction may not matter, but keep it
  identical when replacing to preserve the look.
