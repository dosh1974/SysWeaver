# EwGradientNoise2D

Noise: 2D gradient (Perlin style) noise with quintic interpolation. Zero centred, about [-0.7, 0.7].

## Signature

```glsl
float EwGradientNoise2D(vec2 p)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec2 | Sample position. Each unit is one lattice cell. |

## Returns

`float`, zero centred. With gradients in [-1, 1]^2 the practical range is about [-0.7, 0.7]; the
value is exactly 0 on every lattice point.

## Description

Each lattice corner gets a pseudo random gradient vector `EwHash2To2(corner) * 2.0 - 1.0` (not
normalized, so gradient lengths vary, which slightly softens the result compared to classic
Perlin). The contribution of a corner is `dot(gradient, f - corner)`; the four contributions are
blended with the quintic fade. Compared to [EwValueNoise2D](EwValueNoise2D.md) the features are
less blocky and have more uniform frequency content; compared to
[EwSimplexNoise2D](EwSimplexNoise2D.md) it is cheaper to read but shows faint axis alignment.

Cost: 4 vec2 hashes. To get [0, 1]: `EwGradientNoise2D(p) * 0.7 + 0.5` (clamp if needed), or
scale by ~1.4 for about [-1, 1].

## Requires

- [EwHash2To2](../hash/EwHash2To2.md)

## Example

```glsl
float n = EwGradientNoise2D(uv * 8.0);
vec3 color = vec3(0.5 + 0.7 * n);
```

## Replacing local implementations

iq's `noise` (gradient version) uses `hash` returning `-1.0 + 2.0 * fract(...)` and quintic or
hermite weights - map directly (hermite locals look nearly identical). Local versions that
normalize the gradient or pick from 4/8 fixed directions produce a slightly larger range
(about [-0.7, 0.7] becomes up to ~[-1, 1]); multiply by a constant to match brightness.
Locals that return `0.5 + 0.5 * n` need the same remap applied to this snippet's result.
