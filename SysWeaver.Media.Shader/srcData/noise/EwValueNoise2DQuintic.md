# EwValueNoise2DQuintic

Noise: 2D value noise with quintic interpolation (C2 continuous), in [0, 1].

## Signature

```glsl
float EwValueNoise2DQuintic(vec2 p)
```

## Parameters

| Name | Type | Description |
| :--- | :--- | :--- |
| p | vec2 | Sample position. Each unit is one lattice cell. |

## Returns

`float` in [0, 1].

## Description

Identical to [EwValueNoise2D](EwValueNoise2D.md) except the blend weights use
`f^3 * (f * (f * 6.0 - 15.0) + 10.0)`. Second derivatives are continuous, which matters when the
noise is used as a height field and normals are computed from it (no lighting creases along cell
borders). The cells look slightly flatter in the middle. Cost: 4 hashes.

## Requires

- [EwHash2To1](../hash/EwHash2To1.md)

## Example

```glsl
float height = EwValueNoise2DQuintic(position.xz * 0.5);
```

## Replacing local implementations

Local 2D value noises using `f*f*f*(f*(f*6.0-15.0)+10.0)` map directly; a different local hash
only changes the random pattern. Signed versions: `* 2.0 - 1.0`.
