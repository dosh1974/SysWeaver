# EwSimplexNoise2D

Noise: 2D simplex noise (Inigo Quilez's compact version, MIT), zero centred, about [-1, 1].

## Signature

```glsl
float EwSimplexNoise2D(vec2 p)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec2 | Sample position. Feature size is roughly one unit. |

## Returns

`float`, zero centred, about [-1, 1] (rarely reaches the extremes).

## Description

The plane is skewed with `K1 = (sqrt(3) - 1) / 2` so that the triangular simplex grid becomes a
square grid, the containing triangle is found with a single comparison, and each of the three
corners contributes `max(0.5 - r^2, 0)^4 * dot(gradient, offset)`. The final `* 70.0` scales
the result to about [-1, 1]. Only 3 gradient lookups (vs 4 for
[EwGradientNoise2D](EwGradientNoise2D.md)), no visible axis alignment, isotropic look.

Gradients are `EwHash2To2(corner) * 2.0 - 1.0` (unnormalized, as in iq's original), so the
actual peak is somewhat below 1. To map to [0, 1]: `0.5 + 0.5 * EwSimplexNoise2D(p)`.

## Requires

- [EwHash2To2](../hash/EwHash2To2.md)

## Example

```glsl
float n = 0.5 + 0.5 * EwSimplexNoise2D(uv * 5.0 + iTime * 0.1);
```

## Replacing local implementations

Local `simplexNoise(vec2)` versions with `K1 = 0.366025404`, `K2 = 0.211324865`, `0.5 - dot(...)`,
`h*h*h*h` and `* 70.0` are the same algorithm; only the gradient hash differs (iq's `hash`
returns `-1.0 + 2.0 * fract(sin(...))`). Ashima's `snoise(vec2)` (mod289 / permute, `0.5 - r^2`,
`130.0` scale, `h^4` with normalized gradients) has a similar range and look and can usually be
replaced by this snippet. Locals using a ternary `(x > y) ? vec2(1, 0) : vec2(0, 1)` are
equivalent to the `step` used here (they differ only exactly on the diagonal).
