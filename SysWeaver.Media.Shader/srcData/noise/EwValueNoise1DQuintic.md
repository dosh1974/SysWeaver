# EwValueNoise1DQuintic

Noise: 1D value noise with quintic interpolation (C2 continuous), in [0, 1].

## Signature

```glsl
float EwValueNoise1DQuintic(float x)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| x | float | Sample position. Each unit is one lattice cell. |

## Returns

`float` in [0, 1].

## Description

Same as [EwValueNoise1D](EwValueNoise1D.md) but the fractional part is remapped with
`f^3 * (f * (f * 6.0 - 15.0) + 10.0)` (Perlin's improved fade). First and second derivatives
vanish at lattice points, so motion driven by it has no visible acceleration kinks. Slightly
more "plateau" like than the hermite version. Cost: 2 hashes plus a few multiplies.

## Requires

- [EwHash1To1](../hash/EwHash1To1.md)

## Example

```glsl
float cameraSway = EwValueNoise1DQuintic(iTime * 0.3) * 2.0 - 1.0;
```

## Replacing local implementations

Local 1D noise functions that use `f*f*f*(f*(f*6.0-15.0)+10.0)` map directly. A different
local hash changes the random sequence only. Signed versions: `* 2.0 - 1.0`.
