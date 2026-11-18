# EwFbm3D

Noise: fractal Brownian motion over [EwValueNoise3D](EwValueNoise3D.md) (lacunarity 2.02, gain 0.5).

## Signature

```glsl
float EwFbm3D(vec3 p, int octaves)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec3 | Sample position. The first octave has one lattice cell per unit. |
| octaves | int | Number of octaves to sum, 1..8. Values above 8 behave like 8, values below 1 return 0. |

## Returns

`float` in [0, 1 - 0.5^octaves] (0.9375 for 4 octaves); not normalized, average about half
the maximum.

## Description

`sum += amplitude * EwValueNoise3D(p)` with amplitude starting at 0.5 and halving per octave.
Between octaves `p = p * 2.02 + vec3(17.0, 31.0, 47.0)`: the slightly non integer lacunarity
keeps lattice points of different octaves from coinciding, and the shift moves each octave to an
unrelated region of the noise (which also avoids the symmetric "seam" all octaves would share
around the origin). A 3D rotation (like the 2D [EwFbm2D](EwFbm2D.md) matrix) would hide the
lattice slightly better but costs a mat3 multiply per octave; this variant matches the most
common pattern in the effects (`p * 2.02 + offset` / `p * 2.03 + offset`).

Cost: `octaves` x 8 hashes, so keep octaves low in raymarch loops (3 - 5) and reduce them with
distance. Normalize with `/ (1.0 - pow(0.5, float(octaves)))`.

## Requires

- [EwValueNoise3D](EwValueNoise3D.md)

## Example

```glsl
// Volumetric cloud density
float density = EwFbm3D(position * 0.8 + vec3(iTime * 0.1, 0.0, 0.0), 5) - 0.45;

// Evolving 2D clouds
float clouds = EwFbm3D(vec3(uv * 3.0, iTime * 0.1), 4);
```

## Replacing local implementations

Local 3D fbm variants differ mainly in the octave transform: `p * 2.0`, `p * 2.03 + 11.7`,
`m3 * p * 2.02` (iq's `mat3(0.00, 0.80, 0.60, -0.80, 0.36, -0.48, -0.60, -0.48, 0.64)`) or a time
shift per octave. These change the pattern but not the statistics; replace when an exact match
is not needed. Unrolled 3 / 4 / 5 octave versions map to `EwFbm3D(p, 3 / 4 / 5)`. Locals that
normalize (`/ 0.875`, `/ 0.9375`, dividing by the accumulated weight) need the same factor
applied. Ridged, turbulence or animated per-octave variants should stay local (copy this loop
and adapt it).
