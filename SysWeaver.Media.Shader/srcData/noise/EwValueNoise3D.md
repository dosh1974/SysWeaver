# EwValueNoise3D

Noise: 3D value noise with hermite (smoothstep) interpolation, in [0, 1]. Used for volumetric
clouds, fog density, animated 2D noise (`vec3(uv, time)`) and raymarched displacement.

## Signature

```glsl
float EwValueNoise3D(vec3 p)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec3 | Sample position. Each unit is one lattice cell. |

## Returns

`float` in [0, 1].

## Description

Hashes the 8 corners of the cell with `EwHash3To1` and blends them trilinearly with
`u = f * f * (3.0 - 2.0 * f)`. Cost: 8 hashes, which adds up quickly in volumetric raymarchers
(e.g. 64 steps x 5 fbm octaves = 2560 hashes per pixel); reduce octaves for distant samples.

Using `vec3(uv, iTime * speed)` gives 2D noise that evolves smoothly in place instead of
scrolling. For octave stacking see [EwFbm3D](EwFbm3D.md).

## Requires

- [EwHash3To1](../hash/EwHash3To1.md)

## Example

```glsl
float density = EwValueNoise3D(rayPosition * 1.5 + vec3(0.0, 0.0, iTime * 0.2));
float evolving = EwValueNoise3D(vec3(uv * 4.0, iTime * 0.3));
```

## Replacing local implementations

Many effects use the classic "1D index" variant: `n = i.x + i.y * 57.0 + i.z * 113.0` (or 157.0 /
113.0) and `hash(n)`, `hash(n + 1.0)`, `hash(n + 57.0)`, ... That is the same trilinear hermite
value noise with a different hash; it repeats with a short period in some directions and its
sin-based hash can band on some GPUs. Replacing it with `EwValueNoise3D` changes the pattern, not
the character. Locals using `smoothstep(0.0, 1.0, f)` are identical in interpolation. Signed
versions: `EwValueNoise3D(p) * 2.0 - 1.0`.
