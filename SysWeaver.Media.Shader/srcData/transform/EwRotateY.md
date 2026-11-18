# EwRotateY

Transform: 3D rotation matrix around the Y axis (right handed, for `EwRotateY(a) * v`).

## Signature

```glsl
mat3 EwRotateY(float angle)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| angle | float | Rotation angle in radians. |

## Returns

`mat3` = `mat3(c, 0.0, -s, 0.0, 1.0, 0.0, s, 0.0, c)` with `c = cos(angle)`, `s = sin(angle)` (column major).

## Description

Right handed rotation: `EwRotateY(a) * v` turns +Z towards +X by `a` radians (counter-clockwise when looking from +Y towards
the origin). The Y component is unchanged. Multiplying from the other side, `v * EwRotateY(a)`, uses the transpose
and rotates by `-a`. Rotations combine right to left: `EwRotateY(yaw) * EwRotateX(pitch) * p` applies pitch first.
For an arbitrary axis use [EwRotateAxisAngle](EwRotateAxisAngle.md); the 2D version is [EwRotate2D](EwRotate2D.md)
(`EwRotateY(a) * v` equals rotating the (z, x) pair with `EwRotate2D(a)`).

## Requires

None.

## Example

```glsl
vec3 q = EwRotateY(iTime * 0.3) * p;     // rotate the domain before evaluating an SDF
```

## Replacing local implementations

- `mat3(c, 0.0, -s, 0.0, 1.0, 0.0, s, 0.0, c)`: identical, replace directly.
- The transposed form `mat3(c, 0.0, s, 0.0, 1.0, 0.0, -s, 0.0, c)` rotates the other way for `m * v`: replace with `EwRotateY(-a)`.
- `v * m` (and `p *= m`) uses the transpose: `p *= rot(a)` with the identical matrix equals `p = EwRotateY(-a) * p`.
- Local versions built from a 2D rotation (`p.zx = rot2D(a) * p.zx`) map to EwRotateY only when the
  2D rotation is counter-clockwise in the (z, x) order; check signs and swizzle order (`p.zx` vs reversed) before replacing.
