# EwRotateAxisAngle

Transform: 3D rotation matrix around an arbitrary normalized axis (Rodrigues formula), right handed for `m * v`.

## Signature

```glsl
mat3 EwRotateAxisAngle(vec3 axis, float angle)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| axis | vec3 | Rotation axis. Must be normalized (not checked). |
| angle | float | Rotation angle in radians. |

## Returns

`mat3` rotation matrix `c * I + s * K + (1 - c) * axis * axis^T` (K = cross product matrix of `axis`).

## Description

`EwRotateAxisAngle(axis, a) * v` rotates `v` counter-clockwise by `a` when looking from `+axis` towards the origin
(right hand rule). It matches the single axis snippets: `EwRotateAxisAngle(vec3(1, 0, 0), a) == EwRotateX(a)`,
likewise for Y ([EwRotateY](EwRotateY.md)) and Z ([EwRotateZ](EwRotateZ.md)). `v * m` uses the transpose and rotates by `-a`.
A non normalized axis gives a matrix that scales / shears; normalize it first. One `sin`, one `cos`, a handful of mads.

## Requires

None.

## Example

```glsl
vec3 spinAxis = normalize(vec3(1.0, 1.0, 0.3));
vec3 q = EwRotateAxisAngle(spinAxis, iTime) * p;
```

## Replacing local implementations

- Rodrigues vector form `v * c + cross(k, v) * s + k * dot(k, v) * (1.0 - c)` equals `EwRotateAxisAngle(k, a) * v`.
- Versions with `cross(v, k)` instead of `cross(k, v)`, or the matrix written row by row (transposed, i.e. the
  off diagonal `s` terms with flipped signs), rotate the other way: use `-angle`.
- The common `mat3 rotate(vec3 axis, float angle)` that normalizes inside: keep the `normalize` at the call site.
- `v * m` usage of an identical matrix equals `EwRotateAxisAngle(axis, -angle) * v`.
