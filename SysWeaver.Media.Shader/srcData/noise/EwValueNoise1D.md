# EwValueNoise1D

Noise: 1D value noise. Smoothly interpolates random lattice values with a cubic hermite (smoothstep) curve.

## Signature

```glsl
float EwValueNoise1D(float x)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| x | float | Sample position. Each unit is one lattice cell, scale `x` to change the frequency. |

## Returns

`float` in [0, 1]. Exactly the hash value at integer `x`.

## Description

`floor(x)` selects the lattice cell, `EwHash1To1` provides a random value at each end and the
fractional part is remapped with `f * f * (3.0 - 2.0 * f)` before `mix`. The result is C1
continuous (the derivative is 0 on lattice points, which can show as faint "steps" when the noise
drives motion). Use [EwValueNoise1DQuintic](EwValueNoise1DQuintic.md) for C2 continuity, or
[EwValueNoise1DVec4](EwValueNoise1DVec4.md) to evaluate four channels at once.

Cost: 2 hashes. Precision: for very large `x` (above ~1e5) `fract` loses bits; wrap time inputs.

## Requires

- [EwHash1To1](../hash/EwHash1To1.md)

## Example

```glsl
float flicker = EwValueNoise1D(iTime * 8.0);
float wobble  = EwValueNoise1D(iTime * 0.5 + 13.7) * 2.0 - 1.0;   // signed
```

## Replacing local implementations

Local `noise1D` / `valueNoise1D` / `smoothNoise` functions with `mix(hash(i), hash(i + 1.0), smoothstep(0.0, 1.0, f))`
are equivalent (`smoothstep(0,1,f)` is the same hermite curve). If the local hash differs
(e.g. `fract(sin(x) * 43758.5453)`) the pattern changes but the character is identical.
Versions returning [-1, 1] map to `EwValueNoise1D(x) * 2.0 - 1.0`.
