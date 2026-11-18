# EwPermute289

Noise helper: permutation polynomial `((x * 34 + 1) * x) mod 289` (Ashima Arts / Ian McEwan webgl-noise, MIT).

## Signature

```glsl
float EwPermute289(float x)
vec3  EwPermute289(vec3 x)
vec4  EwPermute289(vec4 x)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| x | float / vec3 / vec4 | Integer valued input(s), normally already in [0, 289). |

## Returns

Same type as `x`, integer valued, in [0, 289).

## Description

The polynomial `(34x^2 + x) mod 289` maps [0, 289) onto itself as a permutation, replacing the
256 entry permutation table of classic Perlin noise. Nesting it hashes multi-dimensional lattice
coordinates: `EwPermute289(EwPermute289(i.z + ...) + i.y + ...) + i.x + ...`. Used by
[EwSimplexNoise3D](EwSimplexNoise3D.md) and Ashima's other noises (cellular, classic Perlin).

The input should be kept in [0, 289) (wrap with [EwMod289](EwMod289.md) first), otherwise
`x * x * 34` exceeds the exact float integer range and the pattern degrades. The quality is
fine for noise, but it is not a general purpose hash: prefer the Hoskins hashes in `hash/` for
random values.

## Requires

- [EwMod289](EwMod289.md)

## Example

```glsl
vec3 cell = EwMod289(floor(position));
float id = EwPermute289(EwPermute289(EwPermute289(cell.x) + cell.y) + cell.z) / 289.0;
```

## Replacing local implementations

Local `permute(x)` / `permute289(x)` from webgl-noise are identical. Some newer webgl-noise
versions use `((x * 34.0) + 10.0) * x` (better distribution); that changes the pattern, keep
the local version if an exact match is required.
