# EwFbm2D

Noise: fractal Brownian motion over [EwValueNoise2D](EwValueNoise2D.md) with rotated octaves
(lacunarity 2, gain 0.5).

## Signature

```glsl
float EwFbm2D(vec2 p, int octaves)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec2 | Sample position. The first octave has one lattice cell per unit. |
| octaves | int | Number of octaves to sum, 1..8. Values above 8 behave like 8, values below 1 return 0. |

## Returns

`float` in [0, 1 - 0.5^octaves] (e.g. 0.9375 for 4 octaves, 0.996 for 8). The result is **not**
normalized; the average is about half the maximum and values near the bounds are rare.

## Description

`sum += amplitude * EwValueNoise2D(p)`, starting with amplitude 0.5 and halving it each octave,
while `p = mat2(1.6, 1.2, -1.2, 1.6) * p` rotates the domain by ~36.87 degrees and doubles the
frequency. The rotation breaks the alignment of the lattices of successive octaves, which
otherwise shows as axis aligned streaks. The loop has a compile time bound of 8 with an early
`break`, so `octaves` may be a uniform or computed (level of detail) value.

Cost: `octaves` x 4 hashes. 4 - 6 octaves are typical for clouds; more than ~6 octaves rarely
adds visible detail at screen resolution unless the base frequency is low.

To normalize to [0, 1]: `EwFbm2D(p, n) / (1.0 - pow(0.5, float(n)))`.

## Requires

- [EwValueNoise2D](EwValueNoise2D.md)

## Example

```glsl
float clouds = EwFbm2D(uv * 3.0 + vec2(iTime * 0.05, 0.0), 5);
clouds = smoothstep(0.35, 0.75, clouds);

// Domain warping (iq)
vec2 warp = vec2(EwFbm2D(uv * 2.0, 4), EwFbm2D(uv * 2.0 + vec2(5.2, 1.3), 4));
float pattern = EwFbm2D(uv * 2.0 + 4.0 * warp, 6);
```

## Replacing local implementations

Effects contain many hand written fbm variants. They all share the structure "sum of scaled
noise, frequency up, amplitude down", but differ in details that change the look:

- **Lacunarity + shift**: `p = p * 2.03 + vec2(1.7, 9.2)` / `p * 2.02 + 7.3` instead of a
  rotation. The non integer lacunarity and the offset also decorrelate octaves. Visually close
  to this snippet; replace if an exact match is not required.
- **Different rotation**: `mat2(0.8, 0.6, -0.6, 0.8) * p * 2.03`, `mat2(1.62, 1.18, -1.18, 1.62)`
  or `mat2(1.6, 1.2, -1.2, 1.6) * p + 3.1`. Same idea, different pattern.
- **Unrolled versions**: fixed 3 or 4 octave code with explicit `0.5000, 0.2500, 0.1250, 0.0625`
  weights maps to `EwFbm2D(p, 3)` / `EwFbm2D(p, 4)`.
- **Normalized versions**: `/ 0.875` (3 octaves), `/ 0.9375` (4 octaves), `* 1.3333` (2 octaves)
  or dividing by an accumulated amplitude sum; apply the same factor to this snippet's result.
- **Signed noise**: locals summing `noise * 2.0 - 1.0` give a zero centred result; use
  `EwFbm2D(p, n) * 2.0 - (1.0 - pow(0.5, float(n)))`.
- **Other gains**: amplitude `* 0.45` / `* 0.55` (rougher or smoother) or ridged
  (`1.0 - abs(n * 2.0 - 1.0)`) / turbulence (`abs(...)`) octaves are different functions; copy
  this snippet into the effect and adapt the loop instead of forcing them onto `EwFbm2D`.
- **Different base noise**: fbm over gradient or simplex noise is zero centred; write a local
  loop with [EwGradientNoise2D](EwGradientNoise2D.md) / [EwSimplexNoise2D](EwSimplexNoise2D.md)
  following the same pattern.
