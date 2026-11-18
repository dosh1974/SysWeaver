# EwValueNoise2D

Noise: 2D value noise with hermite (smoothstep) interpolation, in [0, 1]. The workhorse for fbm
clouds, terrain heights and texture variation.

## Signature

```glsl
float EwValueNoise2D(vec2 p)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec2 | Sample position. Each unit is one lattice cell; multiply to raise the frequency. |

## Returns

`float` in [0, 1] (the average is about 0.5, extremes are rare).

## Description

The four corners of the cell containing `p` get a random value from `EwHash2To1`; the values are
blended bilinearly using `u = f * f * (3.0 - 2.0 * f)`. Cost: 4 hashes. C1 continuous; the grid
can show as axis-aligned "blobs" at low frequency, which is why fbm rotates its octaves
(see [EwFbm2D](EwFbm2D.md)). Alternatives: [EwValueNoise2DQuintic](EwValueNoise2DQuintic.md)
(C2, smoother normals), [EwValueNoise2DDerivatives](EwValueNoise2DDerivatives.md) (analytic
gradient), [EwGradientNoise2D](EwGradientNoise2D.md) / [EwSimplexNoise2D](EwSimplexNoise2D.md)
(less blocky, zero-centred).

Precision: keep `p` below ~1e4 for stable `fract` on mobile GPUs.

## Requires

- [EwHash2To1](../hash/EwHash2To1.md)

## Example

```glsl
float n = EwValueNoise2D(uv * 6.0 + vec2(iTime * 0.2, 0.0));
vec3 color = mix(vec3(0.1, 0.2, 0.4), vec3(0.9), n);
```

## Replacing local implementations

Local `valueNoise2D` / `noise` / `smoothNoise2D` functions of the form
`mix(mix(a, b, u.x), mix(c, d, u.x), u.y)` with `u = f*f*(3.0-2.0*f)` (or `smoothstep(0,1,f)`)
are equivalent. The expanded form `a + (b-a)*u.x + (c-a)*u.y + (a-b-c+d)*u.x*u.y` is identical
too. A different local hash (sin based, or a `p.x + p.y * 57.0` index fed to a 1D hash) changes
the pattern but not the look. Signed versions map to `EwValueNoise2D(p) * 2.0 - 1.0`. Locals
using the quintic curve should use [EwValueNoise2DQuintic](EwValueNoise2DQuintic.md).
