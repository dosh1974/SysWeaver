# EwRotateX

Transform: 3D rotation matrix around the X axis (right handed, for `EwRotateX(a) * v`).

## Signature

```glsl
mat3 EwRotateX(float angle)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| angle | float | Rotation angle in radians. |

## Returns

`mat3` = `mat3(1.0, 0.0, 0.0, 0.0, c, s, 0.0, -s, c)` with `c = cos(angle)`, `s = sin(angle)` (column major).

## Description

Right handed rotation: `EwRotateX(a) * v` turns +Y towards +Z by `a` radians (counter-clockwise when looking from +X towards
the origin). The X component is unchanged. Multiplying from the other side, `v * EwRotateX(a)`, uses the transpose
and rotates by `-a`. Rotations combine right to left: `EwRotateY(yaw) * EwRotateX(pitch) * p` applies pitch first.
For an arbitrary axis use [EwRotateAxisAngle](EwRotateAxisAngle.md); the 2D version is [EwRotate2D](EwRotate2D.md)
(`EwRotateX(a) * v` equals rotating the (y, z) pair with `EwRotate2D(a)`).

## Requires

None.

## Example

```glsl
vec3 q = EwRotateX(iTime * 0.3) * p;     // rotate the domain before evaluating an SDF
```

## Replacing local implementations

- `mat3(1.0, 0.0, 0.0, 0.0, c, s, 0.0, -s, c)`: identical, replace directly.
- The transposed form `mat3(1.0, 0.0, 0.0, 0.0, c, -s, 0.0, s, c)` rotates the other way for `m * v`: replace with `EwRotateX(-a)`.
- `v * m` (and `p *= m`) uses the transpose: `p *= rot(a)` with the identical matrix equals `p = EwRotateX(-a) * p`.
- Local versions built from a 2D rotation (`p.yz = rot2D(a) * p.yz`) map to EwRotateX only when the
  2D rotation is counter-clockwise in the (y, z) order; check signs and swizzle order (`p.yz` vs reversed) before replacing.
