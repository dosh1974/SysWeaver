# EwRotateZ

Transform: 3D rotation matrix around the Z axis (right handed, for `EwRotateZ(a) * v`).

## Signature

```glsl
mat3 EwRotateZ(float angle)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| angle | float | Rotation angle in radians. |

## Returns

`mat3` = `mat3(c, s, 0.0, -s, c, 0.0, 0.0, 0.0, 1.0)` with `c = cos(angle)`, `s = sin(angle)` (column major).

## Description

Right handed rotation: `EwRotateZ(a) * v` turns +X towards +Y by `a` radians (counter-clockwise when looking from +Z towards
the origin). The Z component is unchanged. Multiplying from the other side, `v * EwRotateZ(a)`, uses the transpose
and rotates by `-a`. Rotations combine right to left: `EwRotateY(yaw) * EwRotateX(pitch) * p` applies pitch first.
For an arbitrary axis use [EwRotateAxisAngle](EwRotateAxisAngle.md); the 2D version is [EwRotate2D](EwRotate2D.md)
(`EwRotateZ(a) * v` equals rotating the (x, y) pair with `EwRotate2D(a)`).

## Requires

None.

## Example

```glsl
vec3 q = EwRotateZ(iTime * 0.3) * p;     // rotate the domain before evaluating an SDF
```

## Replacing local implementations

- `mat3(c, s, 0.0, -s, c, 0.0, 0.0, 0.0, 1.0)`: identical, replace directly.
- The transposed form `mat3(c, -s, 0.0, s, c, 0.0, 0.0, 0.0, 1.0)` rotates the other way for `m * v`: replace with `EwRotateZ(-a)`.
- `v * m` (and `p *= m`) uses the transpose: `p *= rot(a)` with the identical matrix equals `p = EwRotateZ(-a) * p`.
- Local versions built from a 2D rotation (`p.xy = rot2D(a) * p.xy`) map to EwRotateZ only when the
  2D rotation is counter-clockwise in the (x, y) order; check signs and swizzle order (`p.xy` vs reversed) before replacing.
